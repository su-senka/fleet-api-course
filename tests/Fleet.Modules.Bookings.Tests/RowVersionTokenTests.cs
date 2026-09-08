using Fleet.Modules.Bookings.Application;

namespace Fleet.Modules.Bookings.Tests;

/// <summary>
/// The token that becomes an <c>ETag</c>.
/// </summary>
/// <remarks>
/// Most of these are about being generous in what the service accepts back: an
/// <c>If-Match</c> header arrives quoted, and may be weak. Making the endpoint strip that itself
/// would be one more thing to get wrong in fourteen different weeks.
/// </remarks>
public sealed class RowVersionTokenTests
{
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(748u)]
    [InlineData(uint.MaxValue)]
    public void A_token_round_trips(uint rowVersion)
    {
        var token = RowVersionToken.Encode(rowVersion);

        Assert.True(RowVersionToken.TryDecode(token, out var decoded));
        Assert.Equal(rowVersion, decoded);
    }

    [Fact]
    public void Different_versions_produce_different_tokens()
    {
        Assert.NotEqual(RowVersionToken.Encode(100), RowVersionToken.Encode(101));
    }

    [Fact]
    public void A_token_is_the_same_on_every_machine()
    {
        // Big-endian, so two instances behind a load balancer issue identical tokens for the same
        // row. Little-endian would work right up until it did not.
        Assert.Equal("AAAC7A==", RowVersionToken.Encode(748));
    }

    [Fact]
    public void A_quoted_etag_is_accepted()
    {
        var token = RowVersionToken.Encode(748);

        Assert.True(RowVersionToken.TryDecode($"\"{token}\"", out var decoded));
        Assert.Equal(748u, decoded);
    }

    [Fact]
    public void A_weak_etag_is_accepted()
    {
        var token = RowVersionToken.Encode(748);

        Assert.True(RowVersionToken.TryDecode($"W/\"{token}\"", out var decoded));
        Assert.Equal(748u, decoded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-base64!")]
    [InlineData("*")]
    [InlineData("AAAC")]          // three bytes, not four
    [InlineData("AAAAAAAAAAA=")]  // too many bytes
    public void Anything_we_did_not_issue_is_rejected(string? token)
    {
        Assert.False(RowVersionToken.TryDecode(token, out _));
    }
}
