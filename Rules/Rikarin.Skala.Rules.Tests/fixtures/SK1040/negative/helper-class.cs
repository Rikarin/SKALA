using System;

// The non-generic `System.Nullable` helper class shares the name and is a different type with no
// short form at all.
public sealed class Comparing {
    public static int Compare(int? left, int? right) => Nullable.Compare(left, right);
}
