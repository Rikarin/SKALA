using System.Collections.Generic; class C { static readonly Dictionary<string,int> map = new() { {"a",System.Environment.TickCount} }; int M(string key) => map[key]; }
