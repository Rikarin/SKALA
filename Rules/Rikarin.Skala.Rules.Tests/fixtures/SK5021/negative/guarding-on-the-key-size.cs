using System;
using System.Security.Cryptography;

// ⚠ The guard somebody writes to satisfy this rule must not be what the rule reports.
public static class Signing {
    public static void Require(RSA signer) {
        if (signer.KeySize < 2048) {
            throw new InvalidOperationException("the key is too small to sign with");
        }
    }
}
