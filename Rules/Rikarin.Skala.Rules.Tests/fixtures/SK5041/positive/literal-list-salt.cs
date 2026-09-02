using System.Security.Cryptography;

public static class Credentials {
    public static byte[] Derive(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 },
            600_000,
            HashAlgorithmName.SHA256,
            32
        );
}
