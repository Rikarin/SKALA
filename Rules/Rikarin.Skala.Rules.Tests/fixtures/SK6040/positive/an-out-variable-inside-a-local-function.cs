using System.Collections.Generic;

public static class Lines {
    public static int CountValid(IEnumerable<string> lines) {
        var total = 0;
        foreach (var line in lines) {
            if (Valid(line)) {
                total++;
            }
        }

        return total;

        static bool Valid(string text) => int.TryParse(text, out var parsed);
    }
}
