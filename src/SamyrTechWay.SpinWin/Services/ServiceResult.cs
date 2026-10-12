namespace SamyrTechWay.SpinWin.Services;

/// <summary>Outcome of an admin command. <see cref="Field"/> names the form field an error belongs to, if any.</summary>
public sealed record ServiceResult(bool Ok, string? Error = null, string? Field = null)
{
    public static readonly ServiceResult Success = new(true);
    public static ServiceResult Fail(string error, string? field = null) => new(false, error, field);
}

public sealed record ServiceResult<T>(bool Ok, T? Value, string? Error = null, string? Field = null)
{
    public static ServiceResult<T> Success(T value) => new(true, value);
    public static ServiceResult<T> Fail(string error, string? field = null) => new(false, default, error, field);
}
