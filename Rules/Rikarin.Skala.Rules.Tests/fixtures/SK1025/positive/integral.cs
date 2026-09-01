using System.Collections.Generic; class C { static readonly Dictionary<int,string> map = new() { {1,"a"}, {2,"b"} }; bool M(int key) => map.ContainsKey(key); }
