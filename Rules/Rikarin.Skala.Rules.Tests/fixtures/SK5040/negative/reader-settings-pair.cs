using System.Xml;

// ⚠ Both facts on `XmlReaderSettings` is SK5009's finding. SK5040 stays silent here so that one
// defect is reported once rather than by two rules at `error` on the same line.
public static class Loader {
    public static XmlReaderSettings Settings() =>
        new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse, XmlResolver = new XmlUrlResolver() };
}
