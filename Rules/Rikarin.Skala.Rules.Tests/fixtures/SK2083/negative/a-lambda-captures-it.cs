using System;
using System.Collections.Generic;

public sealed class Reminders {
    // The capture is a reference that is not a `foreach` subject, and the scan covers the whole
    // member rather than running forward from the declaration.
    public static void Send(Action<Action> defer) {
        var overdue = new List<string>();
        defer(() => overdue.Add("late"));
        foreach (var invoice in overdue) {
            Console.WriteLine(invoice);
        }
    }
}
