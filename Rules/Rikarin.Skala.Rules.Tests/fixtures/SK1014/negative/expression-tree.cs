using System; using System.Linq.Expressions; class C { Expression<Func<int, bool>> M() => x => x > 0 && x < 10; }
