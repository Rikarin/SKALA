// The HMAC overload also takes the key from the caller.
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

public static class Shared {
    public static bool Accept(XmlDocument document, XmlElement signature, byte[] secret) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        using var mac = new HMACSHA256(secret);
        return signed.CheckSignature(mac);
    }
}
