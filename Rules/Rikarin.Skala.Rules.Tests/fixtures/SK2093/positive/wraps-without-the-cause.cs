using System;
using System.IO;

sealed class ImportException : Exception {
    public ImportException(string message) : base(message) { }

    public ImportException(string message, Exception inner) : base(message, inner) { }
}

sealed class Importer {
    public void Simple(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new ImportException("the import failed");
        }
    }

    // Recording it in a log does not put it in the caller's hands, and the caller is who has to act.
    public void LoggedThenDiscarded(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            Console.WriteLine(error);
            throw new ImportException("the import failed");
        }
    }

    // A framework type with the same pair of constructors.
    public void FrameworkType(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new InvalidOperationException("the import failed");
        }
    }

    // The exception is named in a filter and then thrown away anyway.
    public void BehindAFilter(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) when (error.HResult != 0) {
            throw new ImportException("the import failed");
        }
    }
}
