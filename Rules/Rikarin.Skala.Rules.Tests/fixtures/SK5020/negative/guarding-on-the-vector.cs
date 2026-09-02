using System;
using System.Security.Cryptography;

// The guard somebody writes to satisfy this rule must not be what the rule reports.
public static class Sealer {
    public static ICryptoTransform Encryptor(byte[] key, byte[] vector) {
        if (vector.Length != 16) {
            throw new ArgumentException("the vector is the wrong length", nameof(vector));
        }

        using var cipher = Aes.Create();
        cipher.Key = key;
        cipher.IV = vector;
        return cipher.CreateEncryptor();
    }
}
