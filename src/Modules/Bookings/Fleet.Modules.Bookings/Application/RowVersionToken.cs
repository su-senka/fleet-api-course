using System.Buffers.Binary;

namespace Fleet.Modules.Bookings.Application;

/// <summary>
/// Converts a Postgres <c>xmin</c> value to and from the opaque string on the DTO.
/// </summary>
/// <remarks>
/// <para>
/// The point of encoding it is that clients should not be able to reason about it. A bare integer
/// invites somebody to send <c>version + 1</c> and see what happens; base64 of four big-endian
/// bytes looks like what it is - a token to hand back unchanged.
/// </para>
/// <para>
/// Big-endian so the token does not depend on the machine that produced it, which matters the
/// moment two instances of the API sit behind a load balancer.
/// </para>
/// </remarks>
internal static class RowVersionToken
{
    public static string Encode(uint rowVersion)
    {
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, rowVersion);

        return Convert.ToBase64String(buffer);
    }

    /// <summary>
    /// Reads a token back. Returns <c>false</c> for anything that is not one, which the caller
    /// should treat as a failed precondition rather than a malformed request - a client sending a
    /// token we did not issue is a client working from stale information either way.
    /// </summary>
    public static bool TryDecode(string? token, out uint rowVersion)
    {
        rowVersion = 0;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        // An ETag arrives quoted, and a weak one is prefixed W/. Tolerating both here means the
        // endpoint can pass the header value through without picking it apart first.
        var trimmed = token.Trim();
        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        trimmed = trimmed.Trim('"');

        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        if (!Convert.TryFromBase64String(trimmed, buffer, out var written) || written != sizeof(uint))
        {
            return false;
        }

        rowVersion = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        return true;
    }
}
