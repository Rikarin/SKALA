// The loop hands its counter to a helper, so the write does not appear as an assignment anywhere in
// the body. `WrittenInside` counts a `ref` or `out` argument as a write, which is what makes this
// safe.
class C {
    void M(int count) {
        var i = 0;
        while (i < count) {
            Advance(ref i);
        }
    }

    void ByOut(int count) {
        var i = 0;
        while (i < count) {
            Next(out i);
        }
    }

    static void Advance(ref int value) => value++;

    static void Next(out int value) => value = 1;
}
