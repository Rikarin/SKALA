using System.Collections.Generic; class C { static readonly Dictionary<string,int> map = new() { {"a",1} }; void M(string key) { int other; (map[key], other) = (2,3); } }
