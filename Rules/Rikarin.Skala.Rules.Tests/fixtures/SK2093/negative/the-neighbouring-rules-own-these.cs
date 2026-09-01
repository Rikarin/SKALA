using System;
using System.IO;

sealed class ImportException : Exception {
    public ImportException(string message) : base(message) { }

    public ImportException(string message, Exception inner) : base(message, inner) { }
}

sealed class Neighbours {
    // ⚠ SK7092's subject, and the reason this rule is disjoint from it by construction rather than
    // by filter: the clause propagates what it caught, so it is the logged-and-rethrown case and
    // never this one. SK7092 fires here; this rule must not.
    public void LoggedAndRethrown(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            Console.WriteLine(error);
            throw;
        }
    }

    // The same, with the restatement SK2015 reports. Still a propagation, still not this rule.
    public void RethrownByName(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            Console.WriteLine(error);
            throw error;
        }
    }

    // A clause that wraps on one path and propagates on another is capable of propagating, and this
    // rule stands down wherever that is in doubt.
    public void WrapsOnOnePath(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            if (error.HResult == 0) {
                throw new ImportException("the import failed");
            }

            throw;
        }
    }

    // ⚠ SK2014's subject: an empty, filterless clause. That rule requires the block to hold no
    // statement and this one requires a `throw`, so the two cannot both fire on one clause.
    public void Empty(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException) { }
    }
}
