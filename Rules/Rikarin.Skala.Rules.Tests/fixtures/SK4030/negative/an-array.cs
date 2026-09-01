using System.Linq;

public sealed class Registry {
    // An array's Find/Exists/TrueForAll are static members of Array, so the rewrite is not a rename.
    public static bool AnyReady(int[] values) => values.Any(value => value > 0);
}
