using System.Collections.Immutable;
using System.Collections.ObjectModel;

namespace Contoso.Design;

// ⚠ The trap this rule is built around. Every one of these implements `IList<T>` explicitly and
// throws from every mutator, so an interface test would report exactly the types the advice points
// at.
public sealed class Palette {
    public static readonly ImmutableArray<string> Names = ["red", "green"];

    public static readonly ImmutableList<int> Weights = ImmutableList.Create(1, 2);

    public static readonly ReadOnlyCollection<int> Ranks = new([1, 2]);
}
