using System.Security.Cryptography;

// A constant key is a different finding, and this rule declines to claim it.
public static class Sealer {
    public static ICryptoTransform Encryptor() {
        using var cipher = Aes.Create();
        cipher.Key = new byte[32];
        cipher.GenerateIV();
        return cipher.CreateEncryptor();
    }
}
