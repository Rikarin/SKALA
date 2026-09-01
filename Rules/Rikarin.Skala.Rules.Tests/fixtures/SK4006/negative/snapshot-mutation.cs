using System.Linq; using System.Collections.Generic; class C { void M(List<int> values) { foreach (var value in values.ToList()) values.Remove(value); } }
