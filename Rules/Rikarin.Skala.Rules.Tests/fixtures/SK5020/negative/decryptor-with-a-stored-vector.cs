using System.Security.Cryptography;

// The stored vector is the one the record was written with.
public static class Opener {
    public static ICryptoTransform Decryptor(byte[] key, byte[] stored) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = stored;
        return cipher.CreateDecryptor();
    }
}
