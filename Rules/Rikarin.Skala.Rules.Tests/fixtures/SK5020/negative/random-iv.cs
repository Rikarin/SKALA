using System.Security.Cryptography;

// ⚠ `CA5401` reports this, because its question is "is the IV non-default". This rule's question is
// "is the IV predictable", and the answer here is no.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = RandomNumberGenerator.GetBytes(16);
        return cipher.CreateEncryptor();
    }
}
