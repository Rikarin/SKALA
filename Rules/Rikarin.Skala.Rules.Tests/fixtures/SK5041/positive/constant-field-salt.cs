using System.Security.Cryptography;

// `static readonly byte[] Salt = { … }` is a hard-coded value and cannot be anything else.
public static class Credentials {
    static readonly byte[] Salt = { 1, 2, 3, 4, 5, 6, 7, 8 };

    public static byte[] Derive(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, Salt, 600_000, HashAlgorithmName.SHA256, 32);
}
