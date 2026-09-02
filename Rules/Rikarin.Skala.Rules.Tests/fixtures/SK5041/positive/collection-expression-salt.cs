using System;
using System.Security.Cryptography;

// The span overload, so that `[1, 2, …]` has one target type rather than two. A collection
// expression is matched on syntax, because it lowers to an operation kind naming which would pin
// the Roslyn version the rule compiles against.
public static class Credentials {
    public static byte[] Derive(ReadOnlySpan<char> password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, [1, 2, 3, 4, 5, 6, 7, 8], 600_000, HashAlgorithmName.SHA256, 32);
}
