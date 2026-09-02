using System.Security.Cryptography;

// `AesGcm` takes a nonce per call and has no `IV` property at all.
public static class Sealer {
    public static byte[] Seal(byte[] key, byte[] plaintext) {
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MinSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var cipher = new AesGcm(key, tag.Length);
        cipher.Encrypt(nonce, plaintext, ciphertext, tag);
        return ciphertext;
    }
}
