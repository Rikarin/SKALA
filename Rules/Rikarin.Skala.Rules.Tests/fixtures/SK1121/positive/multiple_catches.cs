using System;
using System.IO;

class MultipleCatches {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } catch (IOException) {
                Report();
            } catch (ObjectDisposedException) {
                Report();
            }
        } finally {
            source.Dispose();
        }
    }

    static void Report() {
    }
}
