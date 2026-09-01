using System.Collections.Generic;
class C {
    static readonly Dictionary<string?,int> map = new() { { "a", 1 } };
    int Find(string key) => map[key];
}
