using System.Collections.Generic;

public sealed class Registry {
    readonly Dictionary<int, string> _names = new();

    public void Remember(int id, string name) {
        if (!_names.ContainsKey(id)) {
            _names.Add(id, name);
            System.Console.WriteLine(name);
        }
    }
}
