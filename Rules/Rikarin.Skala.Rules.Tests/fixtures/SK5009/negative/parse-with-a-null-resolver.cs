using System.Xml;

// ⚠ The fixture that carries the rule's whole argument. On .NET Core the default resolver is null,
// so parsing a DTD resolves nothing external and is not XXE. Firing on `DtdProcessing.Parse` alone
// would report, at error severity, a program that legitimately reads documents with entity
// declarations — on a platform where that is not a vulnerability.
public static class Loader {
    public static XmlReaderSettings Settings() =>
        new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null
        };
}
