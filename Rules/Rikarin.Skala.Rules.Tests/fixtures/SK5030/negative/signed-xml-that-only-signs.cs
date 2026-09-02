// Producing a signature is not checking one. `ComputeSignature` and `GetXml` are the
// whole of the signing path and none of it is a finding.
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

public static class Signing {
    public static XmlElement Sign(XmlDocument document, RSA key) {
        var signed = new SignedXml(document) { SigningKey = key };
        var reference = new Reference("");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signed.AddReference(reference);
        signed.ComputeSignature();
        return signed.GetXml();
    }
}
