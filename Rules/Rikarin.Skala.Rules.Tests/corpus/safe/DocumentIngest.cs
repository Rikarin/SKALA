using System.Xml;

namespace Corpus.Safe;

/// <summary>SK5009's twin: both ways of closing it, and the one that was never open.</summary>
public static class DocumentIngest {
    public static XmlReaderSettings Prohibited() =>
        new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        };

    /// <summary>
    /// ⚠ The file that carries the rule's argument. On .NET Core the default resolver is null, so
    /// parsing a DTD resolves nothing external. Reporting this would be reporting a program that is
    /// not vulnerable, on a platform where the one-fact version of the rule is simply wrong.
    /// </summary>
    public static XmlReaderSettings ParsesButResolvesNothing() =>
        new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null
        };

    public static XmlReaderSettings Restricted() =>
        new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlSecureResolver(new XmlUrlResolver(), "https://schemas.example.com/")
        };

    public static XmlReaderSettings ResolverOnly() {
        var settings = new XmlReaderSettings();
        settings.XmlResolver = new XmlUrlResolver();
        return settings;
    }
}
