using System.Security.Cryptography;

// ⚠ The constructors carry SYSLIB0060 and are obsolete on by default — "use the static Pbkdf2
// method instead" — so this half of the population is legacy code. It is covered anyway, because
// the obsoletion says nothing at all about the salt.
#pragma warning disable SYSLIB0060
public static class Credentials {
    public static byte[] Derive(string password) {
        using var derivation = new Rfc2898DeriveBytes(
            password,
            new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 },
            600_000,
            HashAlgorithmName.SHA256
        );

        return derivation.GetBytes(32);
    }
}
#pragma warning restore SYSLIB0060
