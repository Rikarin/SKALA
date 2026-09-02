using System.IO;
using System.Threading;
using System.Threading.Tasks;

// ⚠ #328: the fix appends the parameter *and* passes it to every call that can take one. Two here
// can — `ReadLineAsync` through an appended overload, `Task.Delay` through another — and
// `TextWriter.WriteAsync(string)` cannot, which does not withdraw the finding: a token promises
// cooperative cancellation at the awaits that can honour it, not an abort.
public sealed class Channel {
    readonly TextReader input = TextReader.Null;
    readonly TextWriter output = TextWriter.Null;

    public async Task PumpAsync() {
        var line = await input.ReadLineAsync();
        await output.WriteAsync(line);
        await Task.Delay(1);
    }
}
