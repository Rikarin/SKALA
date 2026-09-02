using System.Security.Cryptography;

// An array of zeros written at the assignment. Nothing can have filled it, so this is the IV.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = new byte[16];
        return cipher.CreateEncryptor();
    }
}
