using System.Security.Cryptography;

// Neither argument is a constant, so the constant test cannot see this — but the key doubling as the
// IV is as fixed as a literal, and it also hands the IV's value to anyone who can read the ciphertext.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var cipher = Aes.Create();
        return cipher.CreateEncryptor(key, key);
    }
}
