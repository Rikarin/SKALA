using System.Collections.Generic;

public sealed class Codes {
    public IEnumerable<int> All(int fallback) {
        foreach (var code in new[] { 200, 204 }) {
            yield return code;
        }

        yield return fallback;
    }
}
