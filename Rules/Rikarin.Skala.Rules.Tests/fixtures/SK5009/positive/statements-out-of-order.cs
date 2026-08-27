using System.Xml;

// The rule collects both facts about one object and does not care which was written first.
public static class Loader {
    public static XmlReaderSettings Settings() {
        var settings = new XmlReaderSettings();
        settings.XmlResolver = new XmlUrlResolver();
        settings.IgnoreWhitespace = true;
        settings.DtdProcessing = DtdProcessing.Parse;
        return settings;
    }
}
