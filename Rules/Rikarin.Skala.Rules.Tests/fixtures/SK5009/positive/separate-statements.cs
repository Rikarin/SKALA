using System.Xml;

public static class Loader {
    public static XmlReaderSettings Settings() {
        var settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Parse;
        settings.XmlResolver = new XmlUrlResolver();
        return settings;
    }
}
