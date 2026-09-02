using System.Xml;

namespace Corpus.Safe;

/// <summary>
///     SK5040's twin: the same four shapes with the re-enable removed the way a reviewer would remove
///     it — the default left alone, the restricted resolver where entities are genuinely needed, and a
///     resolver whose value the rule cannot see.
/// </summary>
public static class DocumentResolvers {
    public static XmlDocument Default(string untrusted) {
        var document = new XmlDocument();
        document.LoadXml(untrusted);
        return document;
    }

    public static XmlDocument ExplicitlyNull(string untrusted) {
        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(untrusted);
        return document;
    }

    /// <summary>⚠ The mitigation for documents that really do reference entities.</summary>
    public static XmlDocument Restricted(string untrusted) {
        var document = new XmlDocument {
            XmlResolver = new XmlSecureResolver(new XmlUrlResolver(), "https://schemas.example.com/")
        };

        document.LoadXml(untrusted);
        return document;
    }

    /// <summary>
    ///     ⚠ The file that carries this rule's boundary with SK5009. `XmlReaderSettings.DtdProcessing`
    ///     defaults to `Prohibit`, so a resolver on that receiver alone fetches nothing — and reporting
    ///     it here would both claim correct code and double-report every SK5009 finding.
    /// </summary>
    public static XmlReaderSettings SettingsResolverOnly() =>
        new XmlReaderSettings { XmlResolver = new XmlUrlResolver() };

    public static XmlDocument FromAVariable(string untrusted, XmlResolver? resolver) {
        var document = new XmlDocument { XmlResolver = resolver };
        document.LoadXml(untrusted);
        return document;
    }

    public static void TextReaderLeftAlone(string path) {
        var reader = new XmlTextReader(path);
        reader.WhitespaceHandling = WhitespaceHandling.None;
        while (reader.Read()) {
        }
    }
}
