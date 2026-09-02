using System.Net;
using System.Xml;

namespace Corpus.Vulnerable;

/// <summary>
///     SK5040 — an external-fetching resolver put back on a receiver that resolves nothing by default.
/// </summary>
/// <remarks>
///     Every method here re-enables external entity resolution deliberately: the safe value on both
///     <c>XmlDocument</c> and <c>XmlTextReader</c> is the <c>null</c> you get by writing nothing.
/// </remarks>
public static class DocumentResolvers {
    public static XmlDocument ViaInitializer(string untrusted) {
        var document = new XmlDocument { XmlResolver = new XmlUrlResolver() };
        document.LoadXml(untrusted);
        return document;
    }

    public static XmlDocument ViaStatements(string untrusted) {
        var document = new XmlDocument();
        document.PreserveWhitespace = true;
        document.XmlResolver = new XmlUrlResolver();
        document.LoadXml(untrusted);
        return document;
    }

    /// <summary>⚠ Credentials do not restrict what is fetched; they decide what is sent along.</summary>
    public static XmlDocument WithCredentials(string untrusted) {
        var document = new XmlDocument {
            XmlResolver = new XmlUrlResolver { Credentials = CredentialCache.DefaultNetworkCredentials }
        };

        document.LoadXml(untrusted);
        return document;
    }

    public static void TextReader(string path) {
        var reader = new XmlTextReader(path);
        reader.XmlResolver = new XmlUrlResolver();
        while (reader.Read()) {
        }
    }
}
