// Run: node --test tests/js   (dev-only; Node is not needed on the host)
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { layoutSegments, targetRotation, segmentIndexAt, angleUnderPointer, edgeMargin, motionPlan, turnsFor, norm } from '../../src/SamyrTechWay.SpinWin/wwwroot/js/app/wheel-math.js';

const configs = {
  balanced: [0.25, 0.25, 0.25, 0.25],
  spec: [0.26, 0.20, 0.10, 0.12, 0.05, 0.02, 0.25],
  tiny: [0.9899, 0.0001, 0.01],
  single: [1],
  two: [0.75, 0.25],
  many: Array.from({ length: 16 }, (_, i) => (i + 1) / 136),
};

for (const [name, probs] of Object.entries(configs)) {
  const items = probs.map((probability, i) => ({ id: i + 1, probability }));
  const segs = layoutSegments(items);

  test(`${name}: wedges tile the full circle in order`, () => {
    assert.equal(segs.length, items.length);
    assert.ok(Math.abs(segs.at(-1).end - 360) < 1e-9);
    segs.forEach((s, k) => {
      assert.ok(s.span > 0);
      if (k > 0) assert.ok(Math.abs(s.start - segs[k - 1].end) < 1e-9);
    });
  });

  test(`${name}: every target lands strictly inside the chosen wedge, away from separators`, () => {
    for (const seg of segs) {
      for (const current of [0, 17.3, 359.9, -720.4, 12345.6]) {
        for (const rand of [0, 0.001, 0.5, 0.999]) {
          const turns = 6;
          const final = targetRotation(current, seg, turns, rand);
          assert.ok(final - current >= turns * 360 - 1e-9, 'spins forward at least the requested turns');
          assert.ok(final - current < (turns + 1) * 360, 'no more than one extra turn');
          const a = angleUnderPointer(final);
          const m = edgeMargin(seg.span) - 1e-9;
          if (seg.span < 360) {
            assert.ok(a >= seg.start + m && a <= seg.end - m, `${a} inside [${seg.start}, ${seg.end}] with margin`);
          }
          assert.equal(segmentIndexAt(final, segs), seg.index);
        }
      }
    }
  });
}

test('tiny probabilities get a visible minimum wedge but keep order', () => {
  const segs = layoutSegments([{ id: 1, probability: 0.9899 }, { id: 2, probability: 0.0001 }, { id: 3, probability: 0.01 }]);
  assert.ok(segs[1].span >= 7 - 1e-9);
  assert.ok(segs[2].span >= 7 - 1e-9);
  assert.ok(segs[0].span > segs[2].span);
});

test('proportional spans when nothing is tiny', () => {
  const segs = layoutSegments([{ id: 1, probability: 0.75 }, { id: 2, probability: 0.25 }]);
  assert.ok(Math.abs(segs[0].span - 270) < 1e-9);
  assert.ok(Math.abs(segs[1].span - 90) < 1e-9);
});

test('motion plan matches velocity at the hand-off and covers the distance', () => {
  for (const seconds of [2, 5.2, 6.8, 12]) {
    const p = motionPlan(3000, seconds);
    assert.ok(Math.abs(p.d1 + p.d2 - 3000) < 1e-6);
    assert.ok(Math.abs((2 * p.d1) / p.t1 - (4 * p.d2) / p.t2) < 1e-6); // power2.in end v == power4.out start v
  }
});

test('turns scale with duration within bounds', () => {
  assert.equal(turnsFor(0.5), 3);
  assert.ok(turnsFor(6) >= 6 && turnsFor(6) <= 8);
  assert.equal(turnsFor(60), 10);
  assert.equal(norm(-30), 330);
});
