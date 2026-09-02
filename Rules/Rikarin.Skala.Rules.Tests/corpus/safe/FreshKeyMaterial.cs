using System;
using System.Security.Cryptography;
using System.Text;

namespace Corpus.Safe;

/// <summary>
///     SK5020's and SK5021's twin: the same nine shapes with the defect removed the way a reviewer
///     would remove it — a generated vector, a vector read off the message, a decrypting call handed
///     the vector it was given, and key sizes at or above the floor.
/// </summary>
public static class FreshKeyMaterial {
    /// <summary>⚠ Not fixed at compile time, so outside the rule even though it is reused.</summary>
    static readonly byte[] Startup = RandomNumberGenerator.GetBytes(16);

    static readonly byte[] Scratch = new byte[16];

    static FreshKeyMaterial() => RandomNumberGenerator.Fill(Scratch);

    public static ICryptoTransform Generated(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.GenerateIV();
        return cipher.CreateEncryptor();
    }

    /// <summary>⚠ The false positive that decided the rule: allocate, then fill.</summary>
    public static ICryptoTransform AllocatedThenFilled(byte[] key) {
        var vector = new byte[16];
        RandomNumberGenerator.Fill(vector);

        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = vector;
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform DrawnAtTheCall(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = RandomNumberGenerator.GetBytes(16);
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform FromAVariable(byte[] key, string seed) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Encoding.UTF8.GetBytes(seed);
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform DecodedFromAVariable(byte[] key, string stored) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Convert.FromBase64String(stored);
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform FromStartup(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Startup;
        return cipher.CreateEncryptor();
    }

    public static ICryptoTransform FromTheScratchBuffer(byte[] key) {
        var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Scratch;
        return cipher.CreateEncryptor();
    }

    /// <summary>⚠ The decrypting side is handed the vector the message arrived with.</summary>
    public static ICryptoTransform Opening(byte[] key) {
        var cipher = Aes.Create();
        return cipher.CreateDecryptor(key, new byte[16]);
    }

    public static ICryptoTransform DistinctArguments(byte[] key, byte[] vector) {
        var cipher = Aes.Create();
        return cipher.CreateEncryptor(key, vector);
    }

    public static RSA Signer() => RSA.Create(2048);

    public static DSA Dsa() => DSA.Create(3072);

    public static RSA WithCspParameters(CspParameters parameters) =>
        new RSACryptoServiceProvider(2048, parameters);

    public static ECDsa Curve() {
        var signer = ECDsa.Create();
        signer.KeySize = 256;
        return signer;
    }

    public static SymmetricAlgorithm Symmetric() {
        var cipher = Aes.Create();
        cipher.KeySize = 128;
        return cipher;
    }

    public static RSA FromSettings(int bits) => RSA.Create(bits);
}
