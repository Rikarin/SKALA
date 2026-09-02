using System.Threading.Tasks;

public static class AsyncReturn {
    // ⚠ An `async` member needs no case of its own, and that is why it has none: the written return
    // type is `Task<int?>` and never `int?`, so the written-type comparison declines it. The same
    // sentence covers an iterator, which cannot carry `return expr;` at all.
    public static async Task<int?> Go(int value) {
        await Task.Yield();
        return new int?(value);
    }
}
