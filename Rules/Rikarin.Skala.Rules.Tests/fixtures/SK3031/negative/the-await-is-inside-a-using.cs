// ⚠ The disjointness fixture. Eliding the `await` here is not an optimisation, it is the bug
// `SK3007` reports: the `using` disposes the stream at the `return`, before the task it produced has
// finished. This rule never sees the shape, because its body must be a single statement and a
// `using` declaration needs one before the return —
// `ElidingTheAwaitInsideAUsing_IsTheBugSk3007Reports` applies the edit by hand and shows `SK3007`
// firing on what comes out.

using System.IO;
using System.Threading.Tasks;

public sealed class Reader {
    public async Task<int> ReadAsync(string path) {
        using var stream = File.OpenRead(path);
        return await stream.ReadAsync(new byte[16], 0, 16);
    }
}
