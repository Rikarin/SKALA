// `CheckSignatureReturningKey` verifies against the embedded key and hands it back for
// the caller to judge. Whether that is a bug depends on the next statement, so the rule
// does not decide it.
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

public static class Inspecting {
    public static bool Accept(XmlDocument document, XmlElement signature, AsymmetricAlgorithm expected) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);

        if (!signed.CheckSignatureReturningKey(out var used)) {
            return false;
        }

        return used.ToXmlString(false) == expected.ToXmlString(false);
    }
}
