// Pure wheel geometry. Angles are degrees, measured clockwise from 12 o'clock in the wheel's own frame.
// The pointer is fixed at 12 o'clock; the wheel disc rotates clockwise by `rotation` degrees.

/**
 * Equal wedges, one per item, in display order. Slice size is presentation only: the server picks the result
 * by the configured odds, the wheel just lands inside the chosen item's slice.
 * @param {{id:number}[]} items in display order
 * @returns {{id:number, index:number, start:number, end:number, mid:number, span:number}[]}
 */
export function layoutSegments(items) {
  const n = items.length;
  if (n === 0) return [];
  const span = 360 / n;
  return items.map((item, index) => {
    const start = index * span;
    const end = index === n - 1 ? 360 : start + span;
    return { id: item.id, index, start, end, mid: (start + end) / 2, span: end - start };
  });
}

/** Normalise any angle to [0, 360). */
export const norm = a => ((a % 360) + 360) % 360;

/** Safe landing margin inside a wedge: never near a separator. */
export const edgeMargin = span => Math.min(span * 0.22, 6);

/** Angle (wheel frame) currently under the 12 o'clock pointer. */
export const angleUnderPointer = rotation => norm(-rotation);

/** Segment index under the pointer for a given rotation. */
export function segmentIndexAt(rotation, segments) {
  const a = angleUnderPointer(rotation);
  for (const s of segments) if (a >= s.start && a < s.end) return s.index;
  return segments.length - 1;
}

/**
 * Final absolute rotation so the pointer lands inside `segment` (with margin), after at least `turns`
 * full clockwise rotations from `current`. `rand` is a [0,1) value choosing the spot inside the safe zone
 * (presentation only — the server already decided the item).
 */
export function targetRotation(current, segment, turns, rand) {
  const margin = edgeMargin(segment.span);
  const landing = segment.start + margin + rand * Math.max(0, segment.span - 2 * margin);
  const wanted = norm(-landing);
  const delta = norm(wanted - norm(current));
  return current + turns * 360 + delta;
}

/**
 * Two-phase motion plan: ease-in acceleration then a long ease-out deceleration, with matched velocity
 * at the hand-off (power2.in → power4.out). Returns distances/durations in degrees/seconds.
 */
export function motionPlan(totalDegrees, totalSeconds) {
  const t1 = Math.min(1.1, Math.max(0.6, totalSeconds * 0.16));
  const t2 = Math.max(0.5, totalSeconds - t1);
  const v = totalDegrees / (t1 / 2 + t2 / 4); // peak angular velocity at the hand-off
  const d1 = (v * t1) / 2;
  return { t1, t2, d1, d2: totalDegrees - d1, peakVelocity: v };
}

/** Number of full turns for a spin of `seconds` (more time → more turns), clamped. */
export const turnsFor = seconds => Math.max(3, Math.min(10, Math.round(3 + seconds * 0.75)));
