using System.Collections.Generic;
using System.Linq;

public sealed class Ledger {
    readonly List<int> amounts = new();

    public IReadOnlyCollection<int> Amounts {
        get { return amounts.ToList(); }
    }
}
