using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    readonly List<string> entries = new();

    // ⚠ The convention the rule enforces is the property's. Parentheses admit the work, which is
    // exactly the shape the fix would move a finding towards.
    public IReadOnlyList<string> GetItems() => entries.ToList();
}
