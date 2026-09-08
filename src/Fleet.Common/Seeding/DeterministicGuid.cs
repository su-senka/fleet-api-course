using System.Security.Cryptography;
using System.Text;

namespace Fleet.Common.Seeding;

/// <summary>
/// Turns a name into a GUID that is the same on every machine, on every run, forever.
/// </summary>
/// <remarks>
/// <para>
/// The seed data has to be reproducible: the same 250 vehicles with the same ids after every
/// <c>make reset</c>, so that a <c>.http</c> file with a hard-coded id keeps working and so that
/// two students comparing screens are looking at the same row.
/// </para>
/// <para>
/// It also lets modules agree on ids without talking to each other. The Bookings seeder needs
/// vehicle ids, but Bookings must not read the Vehicles tables. Both sides call
/// <c>DeterministicGuid.Create("vehicle", 42)</c> and get the same answer.
/// </para>
/// <para>
/// This is only for seed data. Real rows get <see cref="Guid.CreateVersion7"/>.
/// </para>
/// </remarks>
public static class DeterministicGuid
{
    /// <summary>Derives a stable GUID from a scope name and an index, for example ("vehicle", 42).</summary>
    public static Guid Create(string scope, int index) => Create($"{scope}:{index}");

    /// <summary>Derives a stable GUID from an arbitrary name, for example "depot:Praha".</summary>
    public static Guid Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(name), hash);

        Span<byte> uuid = stackalloc byte[16];
        hash[..16].CopyTo(uuid);

        // Stamp the RFC 9562 version (8 - custom) and variant bits, so the result is a
        // well-formed UUID rather than sixteen arbitrary bytes wearing a GUID's clothes.
        uuid[6] = (byte)((uuid[6] & 0x0F) | 0x80);
        uuid[8] = (byte)((uuid[8] & 0x3F) | 0x80);

        // bigEndian: true keeps the bytes in the order they are printed, so the value does not
        // depend on the endianness of the machine that generated it.
        return new Guid(uuid, bigEndian: true);
    }
}
