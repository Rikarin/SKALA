using System.Threading;

// ⚠ The gate that the reference tree bought. The first draft reported this shape on Vixen's
// `VideoPlayer` and the finding was wrong: `Thread.Start` publishes a memory barrier, so everything
// the constructor wrote before it is visible to the new thread — and the type is `sealed`, so there
// is no derived constructor left to run. Nothing changes after the publication, so nothing races.
// The `if` matters too: the start is the last statement of the block, and the block is the last
// statement of the constructor, so the walk has to look through the enclosing levels rather than
// only at the immediate one.
sealed class Decoder {
    readonly int[] frames;

    readonly Thread? worker;

    public Decoder(int capacity, bool threaded) {
        frames = new int[capacity];

        if (threaded) {
            worker = new Thread(Loop) { IsBackground = true };
            worker.Start();
        }
    }

    void Loop() {
        _ = frames.Length;
    }
}
