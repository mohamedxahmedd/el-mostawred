namespace SamyrTechWay.SpinWin.Services;

/// <summary>Accepts only JPEG, PNG and WebP, identified by file signature (never by extension or client MIME type).</summary>
public static class ImageValidator
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    public static async Task<(byte[]? Data, string? ContentType, string? Error)> ReadAsync(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0) return (null, null, "The selected image is empty.");
        if (file.Length > MaxBytes) return (null, null, "Images must be 2 MB or smaller.");

        using var ms = new MemoryStream((int)file.Length);
        await using (var stream = file.OpenReadStream())
        {
            await stream.CopyToAsync(ms, ct);
        }
        if (ms.Length > MaxBytes) return (null, null, "Images must be 2 MB or smaller.");

        var bytes = ms.ToArray();
        var type = DetectContentType(bytes);
        return type is null
            ? (null, null, "Only JPEG, PNG or WebP images are accepted.")
            : (bytes, type, null);
    }
}
