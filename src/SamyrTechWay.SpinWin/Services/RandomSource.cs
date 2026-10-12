using System.Security.Cryptography;

namespace SamyrTechWay.SpinWin.Services;

public interface IRandomSource
{
    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    int NextInt(int maxExclusive);
}

/// <summary>Cryptographically secure source used for every prize decision.</summary>
public sealed class CryptoRandomSource : IRandomSource
{
    public int NextInt(int maxExclusive) => RandomNumberGenerator.GetInt32(maxExclusive);
}
