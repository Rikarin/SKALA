using System.Diagnostics;

// The `Name =` and `Type =` strings carry the same grammar and the same failure.
[DebuggerDisplay("{Label}", Name = "{Missing}")]
sealed class Node {
    public string Label => "node";
}
