// Ordinary XML handling, including the settings SK5009 is about, with no signature in it.
using System.Xml;

public static class Ingest {
    public static XmlReader Open(System.IO.Stream stream) =>
        XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
}
