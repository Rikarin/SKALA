using System.Linq; using System.Collections.Generic; class C { IEnumerable<int> M(int[] values) => from x in values where x > 0 select x; }
