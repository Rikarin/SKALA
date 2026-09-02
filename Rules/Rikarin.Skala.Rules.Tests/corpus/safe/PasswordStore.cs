using System.Security.Cryptography;

namespace Corpus.Safe;

/// <summary>
///     SK5041's twin: the same derivations with the defect removed the way a reviewer would remove it —
///     a salt drawn per credential, a salt read back from the stored record, and the protocol-fixed
///     derivation that is correct with a constant salt.
/// </summary>
public static class PasswordStore {
    /// <summary>⚠ Not fixed at compile time, so outside the rule even though it is reused.</summary>
    static readonly byte[] Startup = RandomNumberGenerator.GetBytes(16);

    /// <summary>
    ///     ⚠ RFC 5869: HKDF's salt is optional and may be fixed and public. It extracts from
    ///     high-entropy keying material rather than from a password, and both ends of a protocol must
    ///     derive the same key from the same inputs — so this is the shape that decided the rule's
    ///     receiver set, and reporting it would assert a vulnerability in code that has none.
    /// </summary>
    static readonly byte[] ProtocolSalt = { 0x53, 0x4b, 0x41, 0x4c, 0x41, 0x2d, 0x76, 0x31 };

    public static (byte[] Salt, byte[] Key) Register(string password) {
        var salt = RandomNumberGenerator.GetBytes(16);
        return (salt, Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32));
    }

    /// <summary>⚠ The verifying side re-derives with the salt the record carried.</summary>
    public static bool Verify(string password, byte[] storedSalt, byte[] storedKey) {
        var derived = Rfc2898DeriveBytes.Pbkdf2(password, storedSalt, 600_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(derived, storedKey);
    }

    /// <summary>⚠ The false positive that decided the rule: allocate, then fill.</summary>
    public static byte[] AllocatedThenFilled(string password) {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);
    }

    public static byte[] FromAParameter(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);

    public static byte[] FromStartup(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, Startup, 600_000, HashAlgorithmName.SHA256, 32);

    public static byte[] TrafficKey(byte[] sharedSecret) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, 32, ProtocolSalt);

    /// <summary>⚠ This overload draws its own salt, so it has no salt argument to be constant.</summary>
#pragma warning disable SYSLIB0060
    public static byte[] GeneratedSalt(string password) {
        using var derivation = new Rfc2898DeriveBytes(password, 16);
        return derivation.GetBytes(32);
    }
#pragma warning restore SYSLIB0060
}
