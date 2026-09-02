// A caller-supplied certificate, fully chain-validated.
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

public static class Envelopes {
    public static bool Accept(XmlDocument document, XmlElement signature, X509Certificate2 trusted) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        return signed.CheckSignature(trusted, false);
    }
}
