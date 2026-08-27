using System.Xml;

// Whether `resolver` is null is a question about another method. The rule needs both facts to be
// explicit and says nothing here.
public static class Loader {
    public static XmlReaderSettings Settings(XmlResolver? resolver) {
        var settings = new XmlReaderSettings();
        settings.DtdProcessing = DtdProcessing.Parse;
        settings.XmlResolver = resolver;
        return settings;
    }
}
