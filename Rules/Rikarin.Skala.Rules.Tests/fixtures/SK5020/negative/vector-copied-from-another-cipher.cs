using System.Security.Cryptography;

// The value comes from a generated vector on another instance. Not a constant, and not the key.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        using var source = Aes.Create();
        source.GenerateIV();

        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = source.IV;
        return cipher.CreateEncryptor();
    }
}
