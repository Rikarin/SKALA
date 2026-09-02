using System.Security.Cryptography;

// ⚠ The false positive that decided the rule never resolves a local. The array creation is written as
// `new byte[16]` and is filled with random bytes one statement later; a rule that followed `vector`
// back to its declaration would report the correct way of writing this.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key) {
        var vector = new byte[16];
        RandomNumberGenerator.Fill(vector);

        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = vector;
        return cipher.CreateEncryptor();
    }
}
