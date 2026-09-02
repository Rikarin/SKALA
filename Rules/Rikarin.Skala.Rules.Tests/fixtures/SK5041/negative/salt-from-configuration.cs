using System.Security.Cryptography;

public sealed class Options {
    public byte[] Salt { get; init; } = System.Array.Empty<byte>();
}

// A salt read from a property is unknowable here, so the rule declines rather than guesses.
public static class Credentials {
    public static byte[] Derive(string password, Options options) =>
        Rfc2898DeriveBytes.Pbkdf2(password, options.Salt, 600_000, HashAlgorithmName.SHA256, 32);
}
