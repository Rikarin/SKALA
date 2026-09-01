using System.Diagnostics.Contracts;

// `[Pure]` also targets a class, a property and a parameter. None of those has a return type to
// contradict, so none of them is examined.
[Pure]
sealed class Reading {
    [Pure]
    public int Value => 1;

    public void Accept([Pure] string input) {
        _ = input;
    }
}
