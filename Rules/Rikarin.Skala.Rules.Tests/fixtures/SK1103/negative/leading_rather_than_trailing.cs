using System.Collections.Generic;

public sealed class Leading {
    // ⚠ The refuted half of the concept. Hoisting a shared *leading* statement puts it above the
    // `if`, where it runs before the condition is evaluated instead of after — a different program
    // whenever either can see the other, and undecidable in general.
    public static void Record(bool retry, List<int> log) {
        if (retry) {
            log.Add(99);
            log.Add(1);
        } else {
            log.Add(99);
            log.Add(2);
        }
    }
}
