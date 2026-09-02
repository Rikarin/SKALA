using System.Security.Cryptography;

// A symmetric cipher configured at 256 bits. Nothing asymmetric is involved.
public static class Sealer {
    public static SymmetricAlgorithm Cipher() {
        var cipher = Aes.Create();
        cipher.KeySize = 256;
        return cipher;
    }
}
