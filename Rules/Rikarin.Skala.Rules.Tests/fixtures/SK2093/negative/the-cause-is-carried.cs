using System;
using System.IO;

sealed class ImportException : Exception {
    public ImportException(string message) : base(message) { }

    public ImportException(string message, Exception inner) : base(message, inner) { }
}

sealed class Carried {
    // The repair, which the rule must be silent on or `skala fix` would loop.
    public void AsInnerException(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new ImportException("the import failed", error);
        }
    }

    // Anywhere inside the construction counts, not only the trailing position.
    public void Interpolated(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new ImportException($"the import failed: {error.Message}");
        }
    }

    public void ThroughAProperty(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new ImportException("the import failed", error.InnerException ?? error);
        }
    }

    // An aggregate is still carrying it.
    public void Aggregated(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            throw new AggregateException("the import failed", error);
        }
    }
}
