using System.Security.Cryptography;
using System.Text;

// The string is not a literal, so what the bytes are is a question about the caller.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key, string seed) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Encoding.UTF8.GetBytes(seed);
        return cipher.CreateEncryptor();
    }
}
