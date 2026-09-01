using System.Diagnostics;

[DebuggerDisplay("{this.Name}")]
sealed class Basket {
    public string Name => "basket";
}
