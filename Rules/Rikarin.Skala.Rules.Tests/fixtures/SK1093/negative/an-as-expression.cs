using System.IO;

public sealed class AsForm {
    public TextWriter Make() {
        var writer = new StringWriter() as TextWriter;
        return writer;
    }
}
