using System.Xml;

public static class Loader {
    public static XmlReaderSettings Settings() =>
        new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
}
