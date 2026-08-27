using System.Xml;

namespace Corpus.Vulnerable;

/// <summary>SK5009 — DTD parsing with a resolver that will go and fetch.</summary>
public static class DocumentIngest {
    public static XmlReaderSettings ViaInitializer() =>
        new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() };

    public static XmlReaderSettings ViaStatements() {
        var settings = new XmlReaderSettings();
        settings.IgnoreComments = true;
        settings.DtdProcessing = DtdProcessing.Parse;
        settings.XmlResolver = new XmlUrlResolver();
        return settings;
    }
}
