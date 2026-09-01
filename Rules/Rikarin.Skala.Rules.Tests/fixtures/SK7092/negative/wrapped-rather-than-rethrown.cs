using System;
using System.IO;

// ⚠ The rule's stated position. Wrapping translates the failure at a boundary and produces one
// record, not two, and logging the original before translating is how detail the translation drops
// survives at all.
public sealed class ImportException : Exception {
    public ImportException(string message, Exception inner) : base(message, inner) { }
}

public sealed class Importer {
    public void Import(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            Console.Error.WriteLine(error);
            throw new ImportException($"the import of {path} failed", error);
        }
    }
}
