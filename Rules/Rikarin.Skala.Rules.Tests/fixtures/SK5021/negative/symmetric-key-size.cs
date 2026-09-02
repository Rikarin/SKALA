using System.Security.Cryptography;

// `KeySize` is declared on `AsymmetricAlgorithm` and on `SymmetricAlgorithm` alike; 128 bits of AES
// is not 128 bits of RSA, and the family test is what keeps them apart.
public static class Sealer {
    public static SymmetricAlgorithm Cipher() {
        var cipher = Aes.Create();
        cipher.KeySize = 128;
        return cipher;
    }
}
