using System.IO;

class DirectiveHead {
    public void Copy(Stream source) {
        try {
#pragma warning disable CA1031
            try {
                source.Flush();
            } catch (IOException) {
                Report();
            }
#pragma warning restore CA1031
        } finally {
            source.Dispose();
        }
    }

    static void Report() {
    }
}
