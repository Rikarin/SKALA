using System.Linq; using System.Collections.Generic; class C { IEnumerable<int> M(int[] values) { foreach (var value in values.ToArray()) yield return value; } }
