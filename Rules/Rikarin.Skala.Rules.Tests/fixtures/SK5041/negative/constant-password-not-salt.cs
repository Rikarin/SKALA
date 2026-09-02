using System.Security.Cryptography;

// The rule asks about the `salt` parameter and nothing else. A constant *password* is a different
// concept with a different id, and claiming it here would widen this one past what it says.
public static class Credentials {
    public static byte[] Derive(byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2("fixed-passphrase", salt, 600_000, HashAlgorithmName.SHA256, 32);
}
