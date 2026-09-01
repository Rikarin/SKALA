using System.Collections.Generic; class C { static readonly Dictionary<string,int> map = new() { {"a",1} }; void M(string key) { map[key] = 2; } }
