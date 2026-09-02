using System.Xml;

// The receiver test walks the base chain rather than comparing a name, so a document type of the
// caller's own — and `XmlDataDocument`, which is the framework's — is covered.
public sealed class AuditDocument : XmlDocument {
}

public static class Loader {
    public static AuditDocument Load(string untrusted) {
        var document = new AuditDocument { XmlResolver = new XmlUrlResolver() };
        document.LoadXml(untrusted);
        return document;
    }
}
