using System;
using System.Collections.Generic;

public sealed class Reminders {
    // A capacity leaves the collection empty and a source collection does not. Declining both is
    // cheaper than telling them apart, and this is the cost.
    public static void Send(IEnumerable<string> invoices) {
        var overdue = new List<string>(invoices);
        foreach (var invoice in overdue) {
            Console.WriteLine(invoice);
        }
    }
}
