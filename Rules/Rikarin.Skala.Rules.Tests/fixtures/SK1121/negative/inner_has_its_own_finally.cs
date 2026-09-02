using System.IO;

// Merging two `finally` blocks puts two statement lists in one scope, and a throw from the
// first would stop the second running at all.
class InnerFinally {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } catch (IOException) {
                Report();
            } finally {
                Report();
            }
        } finally {
            source.Dispose();
        }
    }

    static void Report() {
    }
}
