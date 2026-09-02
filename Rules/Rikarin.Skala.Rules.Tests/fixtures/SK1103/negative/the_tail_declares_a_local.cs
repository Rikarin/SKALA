using System.Collections.Generic;

public sealed class Declaring {
    static int Measure() => 1;

    // ⚠ A declaration inside the moved run escapes the branch with it, which is the outward scope
    // move C# answers with CS0136 whenever a sibling scope holds the same name.
    public static void Record(bool retry, List<int> log) {
        if (retry) {
            log.Add(1);
            var size = Measure();
            log.Add(size);
        } else {
            log.Add(2);
            var size = Measure();
            log.Add(size);
        }
    }
}
