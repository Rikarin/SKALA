// The fix: the caller says which key it trusts, so the check establishes who signed.
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

public static class Receipts {
    public static bool Accept(XmlDocument document, XmlElement signature, AsymmetricAlgorithm trusted) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        return signed.CheckSignature(trusted);
    }
}
