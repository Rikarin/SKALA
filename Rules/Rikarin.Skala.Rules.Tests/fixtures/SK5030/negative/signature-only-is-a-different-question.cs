// `verifySignatureOnly: true` skips chain validation, but the certificate still came
// from the caller rather than from the document. Whether that is wrong depends on where
// the caller got it, which is not a question this rule asks — so it is silence, stated
// rather than forgotten.
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

public static class Pinned {
    public static bool Accept(XmlDocument document, XmlElement signature, X509Certificate2 pinned) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        return signed.CheckSignature(pinned, true);
    }
}
