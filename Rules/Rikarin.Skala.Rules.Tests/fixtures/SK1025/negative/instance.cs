using System.Collections.Generic; class C { readonly Dictionary<string,int> map = new() { {"a",1} }; int M(string key) => map[key]; }
