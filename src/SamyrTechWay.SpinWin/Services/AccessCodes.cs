using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using SamyrTechWay.SpinWin.Models.Entities;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>Customer access codes: 10 characters from a 31-symbol unambiguous alphabet (~49.5 bits), PBKDF2-hashed.</summary>
public sealed class AccessCodes(IPasswordHasher<Customer> hasher)
{
    public const int Length = 10;
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // no 0/O, 1/I/L

    /// <summary>Hash of a random code, used to equalise timing when the phone number is unknown.</summary>
    private readonly Lazy<string> _dummyHash = new(() => hasher.HashPassword(new Customer(), Generate()));

    public static string Generate()
    {
        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        return new string(chars);
    }

    /// <summary>ABCDE23456 → ABCDE-23456 for display.</summary>
    public static string Format(string code) => code.Length == Length ? $"{code[..5]}-{code[5..]}" : code;

    /// <summary>Uppercase, drop separators, map Arabic-Indic digits. Returns null if it cannot be a valid code.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 40) return null;
        var sb = new StringBuilder(Length);
        foreach (var raw in input)
        {
            var ch = raw switch
            {
                >= '٠' and <= '٩' => (char)('0' + (raw - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (raw - '۰')),
                _ => char.ToUpperInvariant(raw),
            };
            if (ch is ' ' or '-' or '_' or ' ') continue;
            if (!Alphabet.Contains(ch)) return null;
            sb.Append(ch);
        }
        return sb.Length == Length ? sb.ToString() : null;
    }

    public string Hash(Customer customer, string code) => hasher.HashPassword(customer, code);

    public bool Verify(Customer customer, string normalizedCode) =>
        hasher.VerifyHashedPassword(customer, customer.AccessCodeHash, normalizedCode) != PasswordVerificationResult.Failed;

    /// <summary>Spend comparable CPU time when no customer matched, so response time does not reveal registered phones.</summary>
    public void VerifyDummy(string normalizedCode) =>
        hasher.VerifyHashedPassword(new Customer(), _dummyHash.Value, normalizedCode);
}
