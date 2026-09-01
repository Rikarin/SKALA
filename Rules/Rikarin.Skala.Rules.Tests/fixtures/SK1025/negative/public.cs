using System.Collections.Generic; class C { public static readonly Dictionary<string,int> map = new() { {"a",1} }; int M(string key) => map[key]; }
