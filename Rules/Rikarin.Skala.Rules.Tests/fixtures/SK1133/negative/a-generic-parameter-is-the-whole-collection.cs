// fixture-option: LangVersion = 14
// `Wrap<T>(T value)` infers `T = int[]` from the call; `Wrap([..source])` has nothing to infer `T` from
// (CS0411), because the collection expression itself has no natural type.
using System.Collections.Generic;
using System.Linq;

public static class Inference {
    static T Wrap<T>(T value) => value;

    public static int[] Head(IEnumerable<int> source) => Wrap(source.ToArray());
}
