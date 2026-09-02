using System;
using System.Security.Cryptography;

public static class Credentials {
    public static byte[] Derive(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String("c2FsdHktc2FsdA=="),
            600_000,
            HashAlgorithmName.SHA256,
            32
        );
}
