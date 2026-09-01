using System.Linq; using System.Collections.Generic; class C { IEnumerable<int> M(int[] values) => values.Where(x => x > 0).AsEnumerable(); }
