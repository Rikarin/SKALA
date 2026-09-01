using System;
using System.Collections.Generic;

public sealed class Reminders {
    public static void Send(List<string> loaded) {
        var overdue = new List<string>();
        overdue = loaded;
        foreach (var invoice in overdue) {
            Console.WriteLine(invoice);
        }
    }
}
