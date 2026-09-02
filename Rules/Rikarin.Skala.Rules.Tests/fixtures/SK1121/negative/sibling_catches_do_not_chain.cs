using System;
using System.IO;

// The nested form routes an exception thrown BY the inner handler to the outer clause.
// Sibling `catch` clauses do not, so merging lets it escape.
class SiblingCatches {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } catch (IOException) {
                throw new InvalidOperationException();
            }
        } catch (InvalidOperationException) {
            Report();
        }
    }

    static void Report() {
    }
}
