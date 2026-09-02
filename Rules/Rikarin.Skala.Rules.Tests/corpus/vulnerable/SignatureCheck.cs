using System.Security.Cryptography.Xml;
using System.Xml;

namespace Corpus.Vulnerable;

/// <summary>SK5030 — the signature is checked against the key the document carries.</summary>
public static class SignatureCheck {
    /// <summary>The plain form: load the signature out of the document, then ask the document.</summary>
    public static bool Accept(XmlDocument document, XmlElement signature) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        return signed.CheckSignature();
    }

    /// <summary>
    ///     The form that reads most like a real gate: an early return on a failed check, and
    ///     everything after it treated as authenticated.
    /// </summary>
    public static string? Subject(XmlDocument assertion, XmlElement signature) {
        var signed = new SignedXml(assertion);
        signed.LoadXml(signature);

        if (!signed.CheckSignature()) {
            return null;
        }

        return assertion.SelectSingleNode("//Subject")?.InnerText;
    }
}
