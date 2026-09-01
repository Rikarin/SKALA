using System.Linq; class C { IQueryable<int> M(IQueryable<int> values) => values.Where(x => x > 0); }
