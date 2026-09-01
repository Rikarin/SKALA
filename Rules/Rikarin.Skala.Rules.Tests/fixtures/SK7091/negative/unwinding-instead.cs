using System;
using System.IO;

// The shape the rule is asking for: the failure propagates, the `finally` runs, and whoever called
// this gets to decide whether the process should end.
public sealed class Importer {
    public void Import(string source) {
        var scratch = Path.GetTempFileName();
        try {
            if (!File.Exists(source)) {
                throw new FileNotFoundException("the import source is missing", source);
            }

            File.Copy(source, scratch, overwrite: true);
        } finally {
            File.Delete(scratch);
        }
    }
}
