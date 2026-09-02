using System.Xml;

// `null` is the platform default and the mitigation. Writing it explicitly is what a reviewer
// does after reading this rule, and reporting it would report the fix.
public static class Loader {
    public static XmlDocument Load(string untrusted) {
        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(untrusted);
        return document;
    }
}
