using System.Collections.Generic; class C { static readonly Dictionary<string,int> map = new() { {"a",1} }; int M() => map.Keys.Count; }
