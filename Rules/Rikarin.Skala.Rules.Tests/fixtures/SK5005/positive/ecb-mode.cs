using System.Security.Cryptography;

public static class Box {
    public static SymmetricAlgorithm Make() {
        var cipher = Aes.Create();
        cipher.Mode = CipherMode.ECB;
        return cipher;
    }
}
