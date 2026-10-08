// fixture-option: LangVersion = 14
using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    static readonly string[] Seed = { "a", "b" };

    public List<string> Names { get; } = Seed.ToList();
}
