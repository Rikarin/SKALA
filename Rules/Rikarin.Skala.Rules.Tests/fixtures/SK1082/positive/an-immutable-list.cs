using System.Collections.Immutable;
using System.Linq;

public sealed class Registry {
    public static string Chosen(ImmutableList<string> entries, int index) => entries.ElementAt(index);
}
