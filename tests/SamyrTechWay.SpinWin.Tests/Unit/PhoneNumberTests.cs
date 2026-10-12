using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Tests.Unit;

public class PhoneNumberTests
{
    [Theory]
    [InlineData("01012345678", "+201012345678")]
    [InlineData("+201012345678", "+201012345678")]
    [InlineData("201012345678", "+201012345678")]
    [InlineData("00201012345678", "+201012345678")]
    [InlineData("0111 234 5678", "+201112345678")]
    [InlineData("012-1234-5678", "+201212345678")]
    [InlineData("(015) 1234 5678", "+201512345678")]
    [InlineData(" +20 10 1234 5678 ", "+201012345678")]
    [InlineData("٠١٠١٢٣٤٥٦٧٨", "+201012345678")] // Arabic-Indic digits
    [InlineData("‎01012345678", "+201012345678")] // pasted LTR mark
    public void Normalizes_egyptian_mobile_variants(string input, string expected)
    {
        Assert.True(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("0101234567")]      // too short
    [InlineData("010123456789")]    // too long
    [InlineData("01312345678")]     // 013 is not a mobile operator
    [InlineData("02012345678")]     // landline-like
    [InlineData("+441012345678")]   // other country code
    [InlineData("1012345678")]      // missing leading zero
    [InlineData("2001012345678")]   // ambiguous 20 + 0 prefix
    [InlineData("0101234567a")]
    [InlineData("010+12345678")]
    [InlineData("+2001012345678")]
    public void Rejects_malformed_numbers(string? input)
    {
        Assert.False(PhoneNumber.TryNormalize(input, out var normalized));
        Assert.Equal("", normalized);
    }

    [Fact]
    public void Formats_and_masks_for_display()
    {
        Assert.Equal("010 1234 5678", PhoneNumber.ToLocalDisplay("+201012345678"));
        Assert.Equal("010 •••• 5678", PhoneNumber.Mask("+201012345678"));
    }
}
