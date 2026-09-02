using System.IO;

class NoInnerCatch {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
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
