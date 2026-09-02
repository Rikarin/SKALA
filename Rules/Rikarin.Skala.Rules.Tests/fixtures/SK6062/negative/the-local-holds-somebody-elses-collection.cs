using System.Collections.Generic;

public static class Borrowed {
    static readonly List<string> Shared = [];

    static List<string> Rent() => Shared;

    public static int Run(IEnumerable<string> items) {
        var borrowed = Rent();
        foreach (var item in items) {
            borrowed.Add(item);
        }

        return 0;
    }
}
