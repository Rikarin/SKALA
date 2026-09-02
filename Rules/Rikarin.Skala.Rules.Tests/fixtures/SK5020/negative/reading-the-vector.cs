using System.Security.Cryptography;

// A read, not a write.
public static class Sealer {
    public static byte[] Publish(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.GenerateIV();
        return cipher.IV;
    }
}
