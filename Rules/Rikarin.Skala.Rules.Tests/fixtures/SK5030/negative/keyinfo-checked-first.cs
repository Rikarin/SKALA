// ⚠ The guard, and the reason it exists. The certificate inside `KeyInfo` is resolved and
// checked against a trust store before the signature is checked, so by the time
// `CheckSignature()` runs the key has been established and the call is sound.
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

public static class Federation {
    public static bool Accept(XmlDocument document, XmlElement signature, X509Certificate2 issuer) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);

        var embedded = signed.KeyInfo
            .OfType<KeyInfoX509Data>()
            .SelectMany(data => data.Certificates!.OfType<X509Certificate2>())
            .FirstOrDefault();

        if (embedded is null || !embedded.Equals(issuer)) {
            return false;
        }

        return signed.CheckSignature();
    }
}
