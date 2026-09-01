// The language's implicit index support would accept this type: an `int` `Count` and an `int`
// indexer is all it asks for. The indexer here is a lookup by handle, not a position.
public sealed class HandleTable {
    public int Count => 0;

    public string this[int handle] => handle.ToString();
}

public sealed class Consumer {
    public string Resolve(HandleTable table) => table[table.Count - 1];
}
