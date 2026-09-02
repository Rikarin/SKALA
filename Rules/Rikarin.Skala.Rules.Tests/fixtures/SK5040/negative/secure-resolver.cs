using System.Xml;

// `XmlSecureResolver` is the restricted resolver — the mitigation for documents that genuinely
// reference entities — and it is excluded by name so the rule never reports what it recommends.
public static class Loader {
    public static XmlDocument Load(string untrusted) {
        var document = new XmlDocument {
            XmlResolver = new XmlSecureResolver(new XmlUrlResolver(), "https://schemas.example.com/")
        };

        document.LoadXml(untrusted);
        return document;
    }
}
