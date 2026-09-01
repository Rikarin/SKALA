using System.Collections.Generic; class C<T> { static readonly Dictionary<string,int> map = new() { {"a",1} }; int M(string key) => map[key]; void Other() { C<int>.map.Clear(); } }
