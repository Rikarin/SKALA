using System;
using System.Security.Cryptography;
using System.Text;

namespace Corpus.Vulnerable;

/// <summary>SK5020 and SK5021 — a fixed initialisation vector, and keys generated too small.</summary>
public static class KeyMaterial {
    static readonly byte[] Vector = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

    public static ICryptoTransform ZeroVector(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = new byte[16];
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform ConstantField(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Vector;
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform FromALiteral(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Encoding.UTF8.GetBytes("0123456789abcdef");
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform DecodedFromALiteral(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Convert.FromBase64String("AQIDBAUGBwgJCgsMDQ4PEA==");
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform TheKeyAgain(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = cipher.Key;
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform KeyPassedTwice(byte[] key) {
        var cipher = Aes.Create();
        return cipher.CreateEncryptor(key, key);
    }

    public static RSA SmallSigner() => RSA.Create(1024);

    public static DSA SmallDsa() => DSA.Create(1024);

    public static RSA SmallWithCspParameters(CspParameters parameters) =>
        new RSACryptoServiceProvider(1024, parameters);

    public static RSA SmallByProperty() {
        var signer = RSA.Create();
        signer.KeySize = 1024;
        return signer;
    }
}
