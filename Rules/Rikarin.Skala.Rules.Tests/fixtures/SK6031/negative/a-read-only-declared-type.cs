using System.Collections.Generic;

namespace Contoso.Design;

// The declaration says read-only. That a caller could cast back to `List<T>` is a dataflow question
// about every holder of the value, and this rule reads declarations.
public sealed class Report {
    public readonly IReadOnlyList<int> Weights = new List<int> { 1, 2 };

    public readonly IReadOnlyDictionary<string, int> Counts = new Dictionary<string, int>();

    public readonly IEnumerable<string> Tags = new[] { "a" };
}
