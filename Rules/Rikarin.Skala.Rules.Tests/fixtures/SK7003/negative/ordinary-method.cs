using System.Collections.Generic;

public sealed class OrdinaryMethod {
    public static Dictionary<string, int> Index(IReadOnlyList<string> names) {
        var result = new Dictionary<string, int>(names.Count);
        for (var i = 0; i < names.Count; i++) {
            result[names[i]] = i;
        }

        return result;
    }
}
