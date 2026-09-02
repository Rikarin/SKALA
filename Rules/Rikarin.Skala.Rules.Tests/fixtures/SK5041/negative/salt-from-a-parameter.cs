using System.Security.Cryptography;

// Whether the caller's salt is fresh is a question about another method.
public static class Credentials {
    public static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);
}
