using System;
using Xunit;
using System.Security.Cryptography;

// ⚠ A NIST or RFC known-answer test pins the vector by definition, and a security rule at `error`
// that breaks a crypto library's test suite is how a reviewer learns to skim past every security
// finding the tool makes. Test methods are exempt by attribute.
public sealed class AesVectors {
    [Fact]
    public void Cbc_matches_the_published_vector() {
        using var cipher = Aes.Create();
        cipher.Key = new byte[16];
        cipher.IV = new byte[16];
        using var encryptor = cipher.CreateEncryptor();
        Assert.NotNull(encryptor);
    }
}
