// ⚠ `=> new()` would hand every caller a different list.
public sealed class Bag {
    public System.Collections.Generic.List<int> Items { get; } = new System.Collections.Generic.List<int>();
}
