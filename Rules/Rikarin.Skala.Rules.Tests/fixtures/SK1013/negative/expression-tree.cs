using System; using System.Linq.Expressions; class C { Expression<Func<int[], bool>> M() => a => a != null && a.Length == 1 && a[0] == 1; }
