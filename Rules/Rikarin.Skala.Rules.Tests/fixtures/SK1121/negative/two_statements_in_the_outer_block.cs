using System.IO;

// The `finally` covers `Prepare()` too; the merged form would not.
class TwoStatements {
    public void Copy(Stream source) {
        try {
            Prepare();
            try {
                source.Flush();
            } catch (IOException) {
                Report();
            }
        } finally {
            source.Dispose();
        }
    }

    static void Prepare() {
    }

    static void Report() {
    }
}
