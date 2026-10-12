using System.Text;

namespace SamyrTechWay.SpinWin.Services;

/// <summary>Egyptian mobile number normalisation. Canonical form: +201XXXXXXXXX (010/011/012/015 operators).</summary>
public static class PhoneNumber
{
    private const string CountryCode = "20";

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input) || input.Length > 32) return false;

        var sb = new StringBuilder(16);
        var trimmed = input.Trim();
        for (var i = 0; i < trimmed.Length; i++)
        {
            var ch = trimmed[i];
            var digit = ToAsciiDigit(ch);
            if (digit is not null)
            {
                sb.Append(digit.Value);
            }
            else if (ch == '+' && i == 0)
            {
                sb.Append('+');
            }
            else if (ch is ' ' or '-' or '(' or ')' or '.' or ' ' or '‎' or '‏')
            {
                // Common separators and bidi marks pasted from phones.
            }
            else
            {
                return false;
            }
        }

        var raw = sb.ToString();
        string national;
        if (raw.StartsWith("+20", StringComparison.Ordinal)) national = "0" + raw[3..];
        else if (raw.StartsWith("0020", StringComparison.Ordinal)) national = "0" + raw[4..];
        else if (raw.StartsWith("20", StringComparison.Ordinal) && raw.Length == 12) national = "0" + raw[2..];
        else if (raw.StartsWith('+')) return false; // other country codes are not supported
        else national = raw;

        // 01 + operator digit (0,1,2,5) + 8 digits
        if (national.Length != 11 || national[0] != '0' || national[1] != '1' || national[2] is not ('0' or '1' or '2' or '5'))
            return false;
        for (var i = 3; i < national.Length; i++)
            if (national[i] is < '0' or > '9') return false;

        normalized = "+" + CountryCode + national[1..];
        return true;
    }

    /// <summary>+201012345678 → 010 1234 5678</summary>
    public static string ToLocalDisplay(string normalized) =>
        normalized.Length == 13 && normalized.StartsWith("+20", StringComparison.Ordinal)
            ? $"0{normalized[3..5]} {normalized[5..9]} {normalized[9..]}"
            : normalized;

    /// <summary>+201012345678 → 010 •••• 5678</summary>
    public static string Mask(string normalized) =>
        normalized.Length == 13 ? $"0{normalized[3..5]} •••• {normalized[9..]}" : "••••";

    private static char? ToAsciiDigit(char ch) => ch switch
    {
        >= '0' and <= '9' => ch,
        >= '٠' and <= '٩' => (char)('0' + (ch - '٠')), // Arabic-Indic
        >= '۰' and <= '۹' => (char)('0' + (ch - '۰')), // Extended Arabic-Indic (Persian)
        _ => null,
    };
}
