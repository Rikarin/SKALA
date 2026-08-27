using System.Collections.Generic;

namespace Skala.Corpus.Arrangement;

// The right half of the precedence: `var` cannot reach a field, a return, or a property initialiser,
// so target-typed `new` is what applies there.
public class NewWinsWhenLhsNamesTheType {
    private readonly List<int> _field = new List<int>();

    private static readonly Dictionary<string, int> Table = new Dictionary<string, int>();

    public List<string> Property { get; } = new List<string>();

    public List<int> Make() {
        return new List<int>();
    }

    public List<int> Expression() => new List<int>();

    public void Assignment() {
        List<int> local;
        local = new List<int>();
        Held = new List<int>();
    }

    public List<int> Held { get; set; }
}
