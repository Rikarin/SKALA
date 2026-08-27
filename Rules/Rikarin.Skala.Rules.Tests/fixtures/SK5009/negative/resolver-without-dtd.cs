using System.Xml;

public static class Loader {
    public static XmlReaderSettings Settings() {
        var settings = new XmlReaderSettings();
        settings.XmlResolver = new XmlUrlResolver();
        return settings;
    }
}
