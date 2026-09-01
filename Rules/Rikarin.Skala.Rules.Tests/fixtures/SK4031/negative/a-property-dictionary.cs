using System;
using System.Collections.Generic;

public sealed class Report {
    // ⚠ A property may hand back a different dictionary on each read, and the fix leaves the
    // indexer's receiver text in place precisely because it must not be re-evaluated.
    Dictionary<string, int> Totals => new();

    public void Write() {
        foreach (var key in this.Totals.Keys) {
            Console.WriteLine(this.Totals[key]);
        }
    }
}
