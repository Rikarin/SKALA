using System.Linq; using System.Threading.Tasks; class C { async Task M(int[] values) { foreach (var value in values.ToArray()) await Task.Delay(value); } }
