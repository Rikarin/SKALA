using System.IO;

// The section's statements are not a BlockSyntax, so the `using` statement's parent is not a block
// and the "last statement of the enclosing block" proof does not apply.
public sealed class Writer {
    public void Write(string path, int kind) {
        switch (kind) {
            case 0:
                using (var stream = File.OpenWrite(path)) {
                    stream.WriteByte(0);
                }

                break;
        }
    }
}
