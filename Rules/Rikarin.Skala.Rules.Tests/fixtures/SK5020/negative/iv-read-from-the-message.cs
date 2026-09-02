using System;
using System.Security.Cryptography;

// The IV travelled with the ciphertext, which is exactly what is supposed to happen.
public static class Opener {
    public static ICryptoTransform Decryptor(byte[] key, ReadOnlySpan<byte> message) {
        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = message.Slice(0, 16).ToArray();
        return cipher.CreateDecryptor();
    }
}
