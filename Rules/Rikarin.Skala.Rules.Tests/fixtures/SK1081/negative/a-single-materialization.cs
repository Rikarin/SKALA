using System.Collections.Generic;
using System.Linq;

public sealed class Registry {
    // One copy is a copy. Whether it is needed is SK4006's question and it is a flow question.
    public static List<int> Ids(IEnumerable<int> source) => source.ToList();
}
