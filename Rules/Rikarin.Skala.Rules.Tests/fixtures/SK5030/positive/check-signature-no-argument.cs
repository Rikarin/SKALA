using System.Security.Cryptography.Xml;
using System.Xml;

public static class Receipts {
    public static bool Accept(XmlDocument document, XmlElement signature) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        return signed.CheckSignature();
    }
}
