using System;
using System.Security.Cryptography;

// `"…"u8` is the shortest way to write a hard-coded salt in modern C#, and it is a
// `ReadOnlySpan<byte>` of fixed content rather than an array creation — so it needs its own case
// and has its own fixture.
public static class Credentials {
    public static byte[] Derive(ReadOnlySpan<char> password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, "my-application-salt"u8, 600_000, HashAlgorithmName.SHA256, 32);
}
