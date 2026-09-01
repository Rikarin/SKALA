using System.Linq; class C { int[] M(int[] values) => values.Where(x => x > 0).Select(x => x + 1).ToArray(); }
