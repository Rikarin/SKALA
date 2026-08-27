using System;
using System.Security.Cryptography;

// ⚠ This is the code written to satisfy the rule. Reporting a mention of the enum member rather
// than an assignment of it would mean firing on the mitigation.
public static class Box {
    public static void Check(SymmetricAlgorithm cipher) {
        if (cipher.Mode == CipherMode.ECB) {
            throw new InvalidOperationException("ECB is not acceptable here.");
        }
    }
}
