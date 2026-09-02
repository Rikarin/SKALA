using System.Security.Cryptography;
using System.Text;

// Deriving the vector from a literal string does not make it unpredictable, it spells it differently.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = Encoding.UTF8.GetBytes("0123456789abcdef");
        return cipher.CreateEncryptor();
    }
}
