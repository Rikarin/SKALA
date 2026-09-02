using System.Xml;

// The receiver decides the rule. A property called `XmlResolver` on somebody else's type is not
// an `XmlDocument` and not an `XmlTextReader`, and its default is whatever that type says.
public sealed class ParserOptions {
    public XmlResolver? XmlResolver { get; set; }
}

public static class Loader {
    public static ParserOptions Options() => new ParserOptions { XmlResolver = new XmlUrlResolver() };
}
