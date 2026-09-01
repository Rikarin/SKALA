using System.Diagnostics;

[DebuggerDisplay("Count = {total}")]
sealed class Basket {
    int count;

    public int Count => count;
}
