using System.IO;

class StatementAfter {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } catch (IOException) {
                Report();
            }

            Report();
        } finally {
            source.Dispose();
        }
    }

    static void Report() {
    }
}
