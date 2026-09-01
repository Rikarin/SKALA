using System.Collections.Generic; partial class C { static readonly Dictionary<string,int> map = new() { {"a",1} }; int M(string key) => map[key]; }
