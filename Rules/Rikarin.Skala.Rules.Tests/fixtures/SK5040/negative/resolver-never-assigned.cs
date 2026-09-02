using System.Xml;

// Constructing a resolver is not the defect; assigning one to a document that resolves nothing by
// default is. This one is handed to an `XmlReader.Create` call through settings that prohibit the
// DTD, so nothing is ever fetched.
public static class Loader {
    public static XmlReader Read(string path) {
        var resolver = new XmlUrlResolver();
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = resolver };
        return XmlReader.Create(path, settings);
    }
}
