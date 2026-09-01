using System.Collections.Generic;

public sealed class Names {
    public List<string> All(string extra) {
        List<string> names = [.. new string[] { "first", "second" }, extra];
        return names;
    }
}
