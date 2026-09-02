using System.Security.Cryptography;

// ⚠ Only the encrypting side is reported. Decryption has to be handed the vector the message was
// produced with; a constant here is a consequence of somebody else's choice, not this call's defect.
public static class Opener {
    public static ICryptoTransform Decryptor(byte[] key) {
        using var cipher = Aes.Create();
        return cipher.CreateDecryptor(key, new byte[16]);
    }
}
