// The other half of the guard: the caller writes the key material into `KeyInfo` itself,
// so the key the check uses is the caller's, not the document's.
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

public static class Outbound {
    public static bool Round(XmlDocument document, XmlElement signature, RSA trusted) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        signed.KeyInfo = new KeyInfo();
        signed.KeyInfo.AddClause(new RSAKeyValue(trusted));
        return signed.CheckSignature();
    }
}
