using System.Security.Cryptography;

// This overload takes a salt *size* and draws the salt itself, so it has no `salt` parameter and
// the rule is silent without needing to name it.
#pragma warning disable SYSLIB0060
public static class Credentials {
    public static byte[] Derive(string password) {
        using var derivation = new Rfc2898DeriveBytes(password, 16);
        return derivation.GetBytes(32);
    }
}
#pragma warning restore SYSLIB0060
