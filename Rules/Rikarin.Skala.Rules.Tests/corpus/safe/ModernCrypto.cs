using System.Security.Cryptography;

namespace Corpus.Safe;

/// <summary>SK5005's twin, plus the two digests that are deliberately outside the rule.</summary>
public static class ModernCrypto {
    public static SymmetricAlgorithm Cbc() {
        var cipher = Aes.Create();
        cipher.Mode = CipherMode.CBC;
        cipher.GenerateIV();
        return cipher;
    }

    public static SymmetricAlgorithm Default() => Aes.Create();

    public static byte[] Digest(byte[] input) => SHA256.HashData(input);

    /// <summary>⚠ Outside the rule by design. See rules.json for SK5005's cut.</summary>
    public static byte[] ContentAddress(byte[] blob) => MD5.HashData(blob);

    /// <summary>⚠ Also outside it: a protocol-mandated digest is not a security control.</summary>
    public static byte[] ProtocolDigest(byte[] input) => SHA1.HashData(input);

    public static void Reject(SymmetricAlgorithm cipher) {
        if (cipher.Mode == CipherMode.ECB) {
            throw new CryptographicException("ECB is not acceptable here.");
        }
    }
}
