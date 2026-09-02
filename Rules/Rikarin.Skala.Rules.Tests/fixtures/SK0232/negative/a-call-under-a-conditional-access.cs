public sealed class Store {
    public int Read(string name, bool cache = true) => name.Length + (cache ? 1 : 0);
}

public static class Peeking {
    // ⚠ `.Read(…)` is the `WhenNotNull` of a conditional access, so its receiver is the node
    // *above* it. Handing the shortened call to `GetSpeculativeSymbolInfo` detaches it from that
    // access, and Roslyn throws a NullReferenceException looking for it — the crash that was live
    // in SK0234's type-argument branch. The finding is given up rather than proved unsafely.
    public static int? Look(Store? store) => store?.Read("name", true);
}
