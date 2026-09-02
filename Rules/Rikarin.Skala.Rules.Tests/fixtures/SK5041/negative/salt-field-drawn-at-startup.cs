using System.Security.Cryptography;

// `= new byte[16]` is the allocate-then-fill shape and a static constructor may well fill it,
// which is exactly what happens here. Only an explicit list of literals counts as a constant.
public static class Credentials {
    static readonly byte[] Salt = new byte[16];

    static Credentials() => RandomNumberGenerator.Fill(Salt);

    public static byte[] Derive(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, Salt, 600_000, HashAlgorithmName.SHA256, 32);
}
