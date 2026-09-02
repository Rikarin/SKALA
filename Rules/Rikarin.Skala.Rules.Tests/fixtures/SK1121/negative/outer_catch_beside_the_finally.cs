using System;
using System.IO;

// ⚠ The fixture that actually REACHES the outer-catch guard. The other two unsound nestings are
// held back by the "the outer must have a finally" requirement before that guard is ever asked,
// so removing it turned nothing red until this file existed.
//
// Merging here would make `catch (IOException)` and `catch (InvalidOperationException)` siblings,
// and sibling clauses do not chain: today an InvalidOperationException thrown by the first handler
// reaches the second, and after the merge it escapes.
class OuterCatchBesideTheFinally {
    public void Copy(Stream source) {
        try {
            try {
                source.Flush();
            } catch (IOException) {
                throw new InvalidOperationException();
            }
        } catch (InvalidOperationException) {
            Report();
        } finally {
            source.Dispose();
        }
    }

    static void Report() {
    }
}
