// The rule matches the declaring type by inheritance, so the very common
// `SignedXml` subclass that overrides `GetIdElement` is covered without naming it.
using System.Security.Cryptography.Xml;
using System.Xml;

public sealed class SignedXmlWithIds : SignedXml {
    public SignedXmlWithIds(XmlDocument document) : base(document) { }

    public override XmlElement GetIdElement(XmlDocument document, string idValue) =>
        document.SelectSingleNode($"//*[@ID='{idValue}']") as XmlElement;
}

public static class Envelopes {
    public static bool Accept(XmlDocument document, XmlElement signature) {
        var signed = new SignedXmlWithIds(document);
        signed.LoadXml(signature);
        return signed.CheckSignature();
    }
}
