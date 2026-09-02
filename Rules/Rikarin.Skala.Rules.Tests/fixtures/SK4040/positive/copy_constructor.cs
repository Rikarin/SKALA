using System.Collections.Generic;

public sealed class Roster {
    readonly List<string> names = new();

    public IEnumerable<string> Names => new List<string>(names);
}
