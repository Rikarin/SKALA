// ⚠ The rule matches the declaring type, not the method name. A zero-argument
// `CheckSignature()` on something that is not a `SignedXml` is somebody else's API.
using System.Xml;

public sealed class ManifestVerifier {
    public bool CheckSignature() => true;
}

public static class Packages {
    public static bool Accept(XmlDocument document) {
        var verifier = new ManifestVerifier();
        return verifier.CheckSignature();
    }
}
