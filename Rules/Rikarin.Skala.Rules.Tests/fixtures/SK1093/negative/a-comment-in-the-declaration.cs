using System.IO;

public sealed class Documented {
    public TextWriter Make() {
        var writer = (TextWriter)/* widened deliberately */new StringWriter();
        return writer;
    }
}
