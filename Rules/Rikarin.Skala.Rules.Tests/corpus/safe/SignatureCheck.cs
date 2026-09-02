using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace Corpus.Safe;

/// <summary>SK5030's twin: the same load, the same branch, with the key established first.</summary>
public static class SignatureCheck {
    /// <summary>The fix — the caller says which key it trusts.</summary>
    public static bool Accept(XmlDocument document, XmlElement signature, AsymmetricAlgorithm trusted) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);
        return signed.CheckSignature(trusted);
    }

    /// <summary>The same early-return gate, with a certificate the caller resolved and validated.</summary>
    public static string? Subject(XmlDocument assertion, XmlElement signature, X509Certificate2 issuer) {
        var signed = new SignedXml(assertion);
        signed.LoadXml(signature);

        if (!signed.CheckSignature(issuer, false)) {
            return null;
        }

        return assertion.SelectSingleNode("//Subject")?.InnerText;
    }

    /// <summary>
    ///     ⚠ The file that carries the rule's one guard. The certificate inside <c>KeyInfo</c> is
    ///     resolved and checked against the expected issuer before the signature is checked, so by the
    ///     time <c>CheckSignature()</c> runs the key has been established and the call is sound. A rule
    ///     that fired on the argument list alone would report this, at <c>error</c>, on code that is
    ///     correct.
    /// </summary>
    public static bool AfterCheckingTheEmbeddedCertificate(
        XmlDocument document,
        XmlElement signature,
        X509Certificate2 issuer
    ) {
        var signed = new SignedXml(document);
        signed.LoadXml(signature);

        var embedded = signed.KeyInfo
            .OfType<KeyInfoX509Data>()
            .SelectMany(data => data.Certificates!.OfType<X509Certificate2>())
            .FirstOrDefault();

        if (embedded is null || !embedded.Equals(issuer)) {
            return false;
        }

        return signed.CheckSignature();
    }

    /// <summary>Producing a signature is not checking one.</summary>
    public static XmlElement Sign(XmlDocument document, RSA key) {
        var signed = new SignedXml(document) { SigningKey = key };
        var reference = new Reference("");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        signed.AddReference(reference);
        signed.ComputeSignature();
        return signed.GetXml();
    }
}
