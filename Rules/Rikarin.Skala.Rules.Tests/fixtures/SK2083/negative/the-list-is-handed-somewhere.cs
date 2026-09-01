using System;
using System.Collections.Generic;

public sealed class Reminders {
    static void Collect(List<string> into) => into.Add("late");

    // One reference that is not a `foreach` subject is enough. The analyzer never has to decide
    // what `Collect` does with it.
    public static void Send() {
        var overdue = new List<string>();
        Collect(overdue);
        foreach (var invoice in overdue) {
            Console.WriteLine(invoice);
        }
    }
}
