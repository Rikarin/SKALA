using System.Collections.Generic; class C { static readonly Dictionary<string,int> map = new() { {"a",1}, {"b",2} }; int M(string key) => map[key]; }
