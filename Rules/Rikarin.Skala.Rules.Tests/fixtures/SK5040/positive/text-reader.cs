using System.Xml;

// `XmlTextReader.DtdProcessing` defaults to `Parse`, so on this receiver the resolver alone
// decides whether an entity is fetched.
public static class Loader {
    public static void Read(string path) {
        var reader = new XmlTextReader(path);
        reader.XmlResolver = new XmlUrlResolver();
        while (reader.Read()) {
        }
    }
}
