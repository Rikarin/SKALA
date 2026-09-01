using System; using System.Linq; class C { void M(int[] values) { Action replace = () => values = new int[0]; foreach (var value in values.ToArray()) replace(); } }
