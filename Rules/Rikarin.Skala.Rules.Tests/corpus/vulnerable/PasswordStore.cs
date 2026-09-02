using System;
using System.Security.Cryptography;
using System.Text;

namespace Corpus.Vulnerable;

/// <summary>SK5041 — PBKDF2 handed a salt that is fixed at compile time.</summary>
/// <remarks>
///     Every method here derives every user's key from the same salt, so one precomputed table breaks
///     the whole store and two users who chose the same password are visibly identical in it.
/// </remarks>
public static class PasswordStore {
    static readonly byte[] Salt = { 1, 2, 3, 4, 5, 6, 7, 8 };

    public static byte[] FromAConstantField(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, Salt, 600_000, HashAlgorithmName.SHA256, 32);

    public static byte[] FromZeros(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(password, new byte[16], 600_000, HashAlgorithmName.SHA256, 32);

    public static byte[] FromALiteralList(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x80 },
            600_000,
            HashAlgorithmName.SHA256,
            32
        );

    public static byte[] FromALiteralString(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            Encoding.UTF8.GetBytes("corpus-application-salt"),
            600_000,
            HashAlgorithmName.SHA256,
            32
        );

    public static byte[] FromABase64Literal(string password) =>
        Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String("c2FsdHktc2FsdA=="),
            600_000,
            HashAlgorithmName.SHA256,
            32
        );

    /// <summary>⚠ The obsolete constructor spelling; SYSLIB0060 says nothing about the salt.</summary>
#pragma warning disable SYSLIB0060
    public static byte[] FromTheConstructor(string password) {
        using var derivation = new Rfc2898DeriveBytes(
            password,
            new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 },
            600_000,
            HashAlgorithmName.SHA256
        );

        return derivation.GetBytes(32);
    }
#pragma warning restore SYSLIB0060
}
