using System.Security.Cryptography;

// The correct shape, and the one the rule's message recommends.
public static class Credentials {
    public static (byte[] Salt, byte[] Key) Derive(string password) {
        var salt = RandomNumberGenerator.GetBytes(16);
        return (salt, Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32));
    }
}
