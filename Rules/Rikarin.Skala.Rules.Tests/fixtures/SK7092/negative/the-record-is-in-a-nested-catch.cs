using System;
using System.IO;

// The inner `catch` records and handles; the outer one propagates and records nothing. Two clauses,
// one record each — the walk is scoped to the clause that owns the node.
public sealed class Importer {
    public void Import(string path, string fallback) {
        try {
            File.ReadAllText(path);
        } catch (IOException outer) {
            try {
                File.ReadAllText(fallback);
            } catch (IOException inner) {
                Console.Error.WriteLine(inner);
                return;
            }

            throw;
        }
    }
}
