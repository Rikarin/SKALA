using System.Collections.Generic; class C { static readonly Dictionary<string,int> map = new() { {"a",1} }; void M() { map.Clear(); } }
