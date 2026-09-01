using System; using System.Collections.Generic; class C { static readonly Dictionary<string,int> map = new(StringComparer.OrdinalIgnoreCase) { {"a",1} }; int M(string key) => map[key]; }
