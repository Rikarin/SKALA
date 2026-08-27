using System.IO;

// Two cousins today — the `foreach` scope and the `using` block — and CS0136 the moment `item`
// is lifted into the method block that encloses both. Nothing at the `using` statement is in
// scope with that name, so only a scan of the whole member finds it.
public sealed class Writer {
    public void Write(string path, int[] values) {
        foreach (var item in values) {
            System.Console.WriteLine(item);
        }

        using (var stream = File.OpenWrite(path)) {
            var item = 2;
            stream.WriteByte((byte)item);
        }
    }
}
