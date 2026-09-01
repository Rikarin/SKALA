using System.Collections.Generic;

public sealed class Codes {
    public int[] All(int fallback) => [.. new List<int> { 200, 204 }, fallback];
}
