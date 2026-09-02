using System.Net;
using System.Xml;

// ⚠ Issue #313 proposed this as a negative and it is a positive. Credentials do not restrict what
// the resolver fetches — the document still names the target — they only decide what is sent
// along, which makes this a request-forgery primitive with the process's authority attached.
public static class Loader {
    public static XmlDocument Load(string untrusted) {
        var document = new XmlDocument {
            XmlResolver = new XmlUrlResolver { Credentials = CredentialCache.DefaultNetworkCredentials }
        };

        document.LoadXml(untrusted);
        return document;
    }
}
