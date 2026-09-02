using System.IO;

// A filter runs in the first pass, before any unwinding, so it is unaffected by the merge.
class CatchWithFilter {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } catch (IOException e) when (e.HResult != 0) {
                Report();
            }
        } finally {
            source.Dispose();
        }
    }

    static void Report() {
    }
}
