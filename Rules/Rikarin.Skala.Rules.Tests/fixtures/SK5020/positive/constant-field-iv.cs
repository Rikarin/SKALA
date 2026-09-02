using System.Security.Cryptography;

// A static field holding an explicit list of literals is a hard-coded IV and cannot be anything else.
public static class Sealer {
    static readonly byte[] Vector = { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, 1, 2, 3, 4, 5, 6 };

    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Vector;
        return cipher.CreateEncryptor();
    }
}
