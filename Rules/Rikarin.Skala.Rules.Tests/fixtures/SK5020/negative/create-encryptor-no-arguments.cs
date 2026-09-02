using System.Security.Cryptography;

// The parameterless overload uses whatever the algorithm was configured with, and nothing here
// configured it with a constant.
public static class Sealer {
    public static ICryptoTransform Encryptor() {
        using var cipher = Aes.Create();
        cipher.GenerateKey();
        cipher.GenerateIV();
        return cipher.CreateEncryptor();
    }
}
