using System;
using System.Collections.Generic;

public sealed class Reminders {
    public static void Send(IEnumerable<string> invoices) {
        var overdue = new List<string>();
        foreach (var invoice in invoices) {
            overdue.Add(invoice);
        }

        foreach (var invoice in overdue) {
            Console.WriteLine(invoice);
        }
    }
}
