using System.Xml;

// The same defect written as a statement rather than an initialiser: different syntax, and the
// rule matches the assignment operation, so both arrive covered.
public static class Loader {
    public static XmlDocument Load(string untrusted) {
        var document = new XmlDocument();
        document.XmlResolver = new XmlUrlResolver();
        document.LoadXml(untrusted);
        return document;
    }
}
