using System.Security.Cryptography;

// The same constant, written as a collection expression.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        return cipher.CreateEncryptor();
    }
}
