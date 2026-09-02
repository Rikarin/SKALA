using System.Security.Cryptography;

// ⚠ The false positive the "written at the argument" discipline exists to avoid: this is a
// correct, freshly drawn salt, and a rule that resolved `salt` to its declaration would see
// `new byte[16]` and report it.
public static class Credentials {
    public static byte[] Derive(string password) {
        var salt = new byte[16];
        RandomNumberGenerator.Fill(salt);
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, 600_000, HashAlgorithmName.SHA256, 32);
    }
}
