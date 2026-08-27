using System.Xml;

// One object parses the DTD, a different one has the resolver. Neither is a finding, and pairing
// them would be an alias-analysis mistake.
public static class Loader {
    public static XmlReaderSettings Documents() {
        var lenient = new XmlReaderSettings();
        lenient.DtdProcessing = DtdProcessing.Parse;

        var fetching = new XmlReaderSettings();
        fetching.XmlResolver = new XmlUrlResolver();

        return lenient;
    }
}
