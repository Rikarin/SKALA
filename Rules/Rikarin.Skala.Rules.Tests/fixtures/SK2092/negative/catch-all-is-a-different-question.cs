using System;

sealed class Boundary {
    // ⚠ `catch (Exception)` catches `NullReferenceException` too, and is deliberately not this rule.
    // A catch-all at a process boundary is a different decision with a different argument, and
    // folding the two together would make the finding unanswerable.
    public int Run(Func<int> work) {
        try {
            return work();
        } catch (Exception error) {
            Console.WriteLine(error);
            return -1;
        }
    }

    public int Bare(Func<int> work) {
        try {
            return work();
        } catch {
            return -1;
        }
    }

    // Every base of the type, and a sibling, all of which would catch it and none of which names it.
    public int Bases(Func<int> work) {
        try {
            return work();
        } catch (SystemException) {
            return -1;
        }
    }
}
