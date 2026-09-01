using System.Diagnostics;

[DebuggerDisplay("{Name,nq} ({Count})")]
sealed class Basket {
    public string Name => "basket";

    public int Count => 0;
}
