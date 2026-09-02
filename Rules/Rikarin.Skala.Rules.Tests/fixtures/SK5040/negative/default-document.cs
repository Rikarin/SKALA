using System.Xml;

// The default is already safe on this platform, so the ordinary spelling has nothing to report.
public static class Loader {
    public static XmlDocument Load(string untrusted) {
        var document = new XmlDocument();
        document.LoadXml(untrusted);
        return document;
    }
}
