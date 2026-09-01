using System;
using System.Collections.Generic;
using System.IO;

// Handing the exception to something that is not a log sink is not recording it. `Add` is not in
// the vocabulary, and the vocabulary is only ever consulted for a call that was given the caught
// exception in the first place.
public sealed class BatchImporter {
    readonly List<Exception> failures = [];

    public void Import(string path) {
        try {
            File.ReadAllText(path);
        } catch (IOException error) {
            failures.Add(error);
            throw;
        }
    }
}
