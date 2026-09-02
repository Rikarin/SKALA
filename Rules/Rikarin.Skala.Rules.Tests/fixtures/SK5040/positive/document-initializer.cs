using System.Xml;

public static class Loader {
    public static XmlDocument Load(string untrusted) {
        var document = new XmlDocument { XmlResolver = new XmlUrlResolver() };
        document.LoadXml(untrusted);
        return document;
    }
}
