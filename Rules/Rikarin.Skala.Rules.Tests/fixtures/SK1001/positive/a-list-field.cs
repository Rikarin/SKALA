using System.Collections.Generic;

public sealed class Names {
    static readonly List<int> Ids = new List<int> { 1, 2, 3 };

    public int Count => Ids.Count;
}
