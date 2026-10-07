// A documentation `cref` names the member it means; there is no receiver whose type supplies a name.
using System;

public static class Docs {
    /// <summary>Returns <see cref="ValueTuple{T1, T2}.Item1" /> of the pair.</summary>
    public static int First((int Count, string Name) pair) => pair.Count;
}
