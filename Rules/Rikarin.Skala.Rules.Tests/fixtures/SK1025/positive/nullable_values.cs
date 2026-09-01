using System.Collections.Generic;
class C {
    static readonly Dictionary<string,string?> map = new() { { "a", null }, { "b", "value" } };
    string? Find(string key) => map[key];
}
