using System;
using System.Collections.Generic;

public sealed class Reminders {
    readonly List<string> overdue = [];

    public void Add(string invoice) => overdue.Add(invoice);

    // A field can be filled by any member of the type and by anything holding the instance.
    public void Send() {
        foreach (var invoice in overdue) {
            Console.WriteLine(invoice);
        }
    }
}
