using System.Security.Cryptography;

// Where the vector came from is the caller's business and this call site cannot see it.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key, byte[] vector) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = vector;
        return cipher.CreateEncryptor();
    }
}
