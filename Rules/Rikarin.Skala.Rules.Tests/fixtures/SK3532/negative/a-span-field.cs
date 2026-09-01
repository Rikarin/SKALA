using System;

public ref struct Reader {
    Span<char> buffer;

    public Reader(Span<char> buffer) {
        this.buffer = buffer;
    }

    public int Length => buffer.Length;
}
