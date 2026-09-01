using System; using System.Linq.Expressions; using System.Collections.Generic; class C { static readonly Dictionary<string,int> map = new() { {"a",1} }; Expression<Func<int>> M() => () => map["a"]; }
