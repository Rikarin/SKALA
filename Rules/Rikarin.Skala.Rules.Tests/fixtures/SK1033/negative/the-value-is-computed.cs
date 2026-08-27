using System.Collections.Generic;

// ⚠ The one that nearly shipped wrong. `if (!d.ContainsKey(k)) d[k] = Build();` calls `Build()`
// only when the key is absent; `d.TryAdd(k, Build())` calls it every time, because C# evaluates
// arguments before the call, and then discards the result when the key was already there.
//
// Both of the Vixen occurrences this guard removed were real: one was `mesh.AddPosition(…)`, which
// mutates the mesh it is called on, and one was `edited.ToMeshData(…)`, which builds a mesh. The
// first would have changed what the program does and the second would have added an allocation to
// the path that already had the key.
public sealed class Meshes {
    readonly Dictionary<int, int> _copies = new();
    int _next;

    int AddPosition(int corner) {
        _next++;
        return corner + _next;
    }

    public void Copy(int corner) {
        if (!_copies.ContainsKey(corner)) {
            _copies[corner] = AddPosition(corner);
        }
    }
}
