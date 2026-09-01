using System.Collections.Generic; class Key { } class C { static readonly Dictionary<Key,int> map = new() { {new Key(),1} }; int M(Key key) => map[key]; }
