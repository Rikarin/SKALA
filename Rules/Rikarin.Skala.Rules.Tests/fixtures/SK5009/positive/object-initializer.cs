using System.Xml;

public static class Loader {
    public static XmlReaderSettings Settings() =>
        new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlUrlResolver()
        };
}
