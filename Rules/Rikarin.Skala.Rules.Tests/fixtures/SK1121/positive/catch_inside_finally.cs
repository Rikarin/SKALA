using System.IO;

class CatchInsideFinally {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } catch (IOException) {
                Report();
            }
        } finally {
            source.Dispose();
        }
    }

    static void Report() {
    }
}
