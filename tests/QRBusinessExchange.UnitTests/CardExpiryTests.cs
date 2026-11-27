using Qrbound.Payments;

namespace QRBusinessExchange.UnitTests;

/// <summary>
/// CardPointe's tokenizer joins year and month without padding (YYYYM for January to September,
/// YYYYMM for October to December); the auth API and qrbound's checks take MMYY.
/// </summary>
public class CardExpiryTests
{
    [Theory]
    [InlineData("20281", "0128")]   // January 2028, as the tokenizer sends it
    [InlineData("20299", "0929")]
    [InlineData("202810", "1028")]
    [InlineData("202812", "1228")]
    [InlineData("202801", "0128")]  // a padded month works too
    [InlineData("1228", "1228")]    // already MMYY
    [InlineData("20280", "20280")]  // month 0: left for the server to reject
    [InlineData("202813", "202813")]
    [InlineData("abcde", "abcde")]
    public void Tokenizer_expiry_becomes_MMYY(string tokenizer, string expected) =>
        Assert.Equal(expected, QrboundCardPayment.ExpiryAsMmYy(tokenizer));
}
