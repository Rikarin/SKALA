using Xunit;
using System.Security.Cryptography;

// ⚠ Generating a deliberately small key to keep a test fast is real and correct. Test methods are
// exempt by attribute, the same test five other rules already use.
public sealed class SignatureRoundTrip {
    [Fact]
    public void A_signature_verifies() {
        using var signer = RSA.Create(512);
        var payload = new byte[] { 1, 2, 3 };
        var signature = signer.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        Assert.True(signer.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }
}
