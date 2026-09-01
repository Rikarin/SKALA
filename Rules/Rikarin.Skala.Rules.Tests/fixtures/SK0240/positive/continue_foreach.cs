using System.Collections.Generic;

class C {
    public static void Run(IEnumerable<string> values) {
        foreach (var value in values) {
            Use(value);
            continue;
        }
    }

    static void Use(string value) { }
}
