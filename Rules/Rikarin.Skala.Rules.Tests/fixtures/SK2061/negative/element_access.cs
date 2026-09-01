// An indexer is a call, so two reads of `items[i]` are two calls.
class C {
    bool M(int[] items, int i) => items[i] == items[i];
}
