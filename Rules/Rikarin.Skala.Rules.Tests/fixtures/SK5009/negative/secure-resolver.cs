using System.Xml;

// `XmlSecureResolver` is the restricted resolver — the mitigation, not the vulnerability, and the
// rule excludes it by name so that it never reports the thing it recommends.
public static class Loader {
    public static XmlReaderSettings Settings() =>
        new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlSecureResolver(new XmlUrlResolver(), "https://schemas.example.com/")
        };
}
