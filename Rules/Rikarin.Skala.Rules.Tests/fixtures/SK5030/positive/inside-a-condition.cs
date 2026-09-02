using System.Security.Cryptography.Xml;
using System.Xml;

public static class Assertions {
    public static void Handle(XmlDocument document, XmlElement signature) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);

        if (!signed.CheckSignature()) {
            return;
        }

        Trust(document);
    }

    static void Trust(XmlDocument document) { }
}
