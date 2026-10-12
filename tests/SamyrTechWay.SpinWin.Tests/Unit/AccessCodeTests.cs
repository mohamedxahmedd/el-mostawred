using Microsoft.AspNetCore.Identity;
using SamyrTechWay.SpinWin.Models.Entities;
using SamyrTechWay.SpinWin.Services;

namespace SamyrTechWay.SpinWin.Tests.Unit;

public class AccessCodeTests
{
    [Fact]
    public void Generated_codes_use_the_unambiguous_alphabet_and_length()
    {
        var codes = Enumerable.Range(0, 500).Select(_ => AccessCodes.Generate()).ToList();
        Assert.All(codes, c =>
        {
            Assert.Equal(AccessCodes.Length, c.Length);
            Assert.All(c, ch => Assert.Contains(ch, AccessCodes.Alphabet));
        });
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.DoesNotContain('0', AccessCodes.Alphabet);
        Assert.DoesNotContain('O', AccessCodes.Alphabet);
        Assert.DoesNotContain('1', AccessCodes.Alphabet);
        Assert.DoesNotContain('I', AccessCodes.Alphabet);
    }

    [Theory]
    [InlineData("abcde-23456", "ABCDE23456")]
    [InlineData(" ABCDE 23456 ", "ABCDE23456")]
    [InlineData("ABCDE٢٣٤٥٦", "ABCDE23456")]
    public void Normalizes_user_input(string input, string expected) =>
        Assert.Equal(expected, AccessCodes.Normalize(input));

    [Theory]
    [InlineData("ABCDE2345")]   // too short
    [InlineData("ABCDE234567")] // too long
    [InlineData("ABCDE2345O")]  // ambiguous letter not in alphabet
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_codes(string? input) => Assert.Null(AccessCodes.Normalize(input));

    [Fact]
    public void Hash_is_salted_and_verifies_only_the_right_code()
    {
        var codes = new AccessCodes(new PasswordHasher<Customer>());
        var customer = new Customer();
        var code = AccessCodes.Generate();
        customer.AccessCodeHash = codes.Hash(customer, code);

        Assert.DoesNotContain(code, customer.AccessCodeHash);
        Assert.NotEqual(customer.AccessCodeHash, codes.Hash(customer, code)); // salted
        Assert.True(codes.Verify(customer, code));
        Assert.False(codes.Verify(customer, AccessCodes.Generate()));
    }

    [Fact]
    public void Format_groups_for_readability() => Assert.Equal("ABCDE-23456", AccessCodes.Format("ABCDE23456"));
}
