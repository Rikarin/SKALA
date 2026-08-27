using System.Collections.Generic;
using System.IO;

// `parsed` is declared by an `out var` in the `using` block's own declaration space — no
// LocalDeclarationStatement anywhere — and by a sibling `if` block. Two cousins today; CS0136 the
// moment the braces go and `parsed` lands in the method block beside them.
public sealed class Writer {
    public void Write(string path, Dictionary<string, int> map, string key) {
        if (key.Length > 0) {
            var parsed = 1;
            System.Console.WriteLine(parsed);
        }

        using (var stream = File.OpenWrite(path)) {
            if (map.TryGetValue(key, out var parsed)) {
                stream.WriteByte((byte)parsed);
            }
        }
    }
}
