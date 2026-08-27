using System;
using System.IO;
using System.Threading.Tasks;

public sealed class Buffered {
    // CS4013: a byref-like local cannot live across an `await`, so this method cannot become async
    // without the local moving out of it first — a refactor rather than an edit.
    public Task<string> ReadAsync(string path) {
        Span<char> scratch = stackalloc char[8];
        scratch[0] = 'x';
        using (var reader = new StreamReader(path)) {
            return reader.ReadToEndAsync();
        }
    }
}
