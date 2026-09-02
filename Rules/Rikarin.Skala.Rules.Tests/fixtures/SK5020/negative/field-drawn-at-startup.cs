using System.Security.Cryptography;

// ⚠ Reusing one random vector for every message is a real weakness, and it is not this rule's: the
// value is not fixed at compile time, so the rule cannot prove it and says nothing.
public static class Sealer {
    static readonly byte[] Vector = RandomNumberGenerator.GetBytes(16);

    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Vector;
        return cipher.CreateEncryptor();
    }
}
