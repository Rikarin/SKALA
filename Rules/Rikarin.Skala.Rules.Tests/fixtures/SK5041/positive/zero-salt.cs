using System.Security.Cryptography;

// `new byte[16]` written at the argument: allocated and never filled, so it is sixteen zeros.
public static class Credentials {
    public static byte[] Derive(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, new byte[16], 600_000, HashAlgorithmName.SHA256, 32);
}
