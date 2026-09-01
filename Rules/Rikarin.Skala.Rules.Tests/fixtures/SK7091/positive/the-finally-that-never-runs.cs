using System;
using System.IO;

// The cleanup two lines below the call is the whole finding: the temporary file survives the run,
// and nothing in the source says the `finally` was skipped.
public sealed class Importer {
    public void Import(string source) {
        var scratch = Path.GetTempFileName();
        try {
            if (!File.Exists(source)) {
                Environment.Exit(2);
            }

            File.Copy(source, scratch, overwrite: true);
        } finally {
            File.Delete(scratch);
        }
    }
}
