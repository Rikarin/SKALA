namespace Vendor.Xml;

// A type of the caller's own that happens to be spelled `XmlDocument`. The rule resolves
// `System.Xml.XmlDocument` through the compilation rather than comparing names, so this is not it.
public class XmlResolver {
}

public sealed class XmlUrlResolver : XmlResolver {
}

public sealed class XmlDocument {
    public XmlResolver? XmlResolver { get; set; }
}

public static class Loader {
    public static XmlDocument Load() => new XmlDocument { XmlResolver = new XmlUrlResolver() };
}
