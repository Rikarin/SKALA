using System.Xml;

// Whether `resolver` is null is a question about another method. Following it is the
// inter-procedural analysis doc 08 puts out of scope, and guessing at `error` is the failure mode
// this range cannot have.
public static class Loader {
    public static XmlDocument Load(string untrusted, XmlResolver? resolver) {
        var document = new XmlDocument { XmlResolver = resolver };
        document.LoadXml(untrusted);
        return document;
    }
}
