// ⚠ A type declaring its own `this[Index]` receives the `Index` value whole and may give it any
// meaning it likes — this ring buffer reads `^0` as the write head, which is a position it very much
// has. The guard is that the access must resolve to an indexer taking an `int`, which is the
// compiler lowering `^0` through the `Length`/`Count` pattern rather than handing it over intact.
using System;

class Ring {
    readonly int[] slots = new int[8];

    public int this[Index index] => this.slots[index.Value % 8];
}

class C {
    int Head(Ring ring) => ring[^0];
}
