using System.Xml;

// ⚠ The boundary with SK5009. `XmlReaderSettings.DtdProcessing` defaults to `Prohibit`, so a
// resolver on this receiver with the default processing fetches nothing — this is correct code,
// and SK5040 must not claim it.
public static class Loader {
    public static XmlReaderSettings Settings() => new XmlReaderSettings { XmlResolver = new XmlUrlResolver() };
}
