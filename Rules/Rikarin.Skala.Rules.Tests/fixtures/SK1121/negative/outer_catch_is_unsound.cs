using System.IO;

// ⚠ Issue #109's own headline example, and it is the nesting that CANNOT be merged: run it and
// the nested form logs body -> finally -> catch while the merged form logs body -> catch -> finally.
class OuterCatch {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } finally {
                source.Dispose();
            }
        } catch (IOException) {
            Report();
        }
    }

    static void Report() {
    }
}
