using System.IO;

class CommentedNeck {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } catch (IOException) {
                Report();
            }

            // the cleanup below has to run even when Report() throws
        } finally {
            source.Dispose();
        }
    }

    static void Report() {
    }
}
