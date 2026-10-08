// fixture-option: LangVersion = 14
using System.Linq;

public sealed class Codes {
    static readonly int[] Source = { 200, 204 };
    static readonly int[] Copy = Source.ToArray();

    public int First => Copy[0];
}
