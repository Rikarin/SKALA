using System.Collections.Generic;

// The shape as it occurs in the wild: a local dictionary and a loop variable as the key. The value
// is a name that is already computed, which is what makes evaluating it unconditionally free.
public sealed class Mesh {
    public IEnumerable<int> CornersOf(int face) {
        yield return face;
    }
}

public sealed class Operations {
    public int Detach(Mesh mesh, int[] region, int fallback) {
        var copies = new Dictionary<int, int>();

        foreach (var face in region) {
            foreach (var corner in mesh.CornersOf(face)) {
                if (!copies.ContainsKey(corner)) {
                    copies[corner] = fallback;
                }
            }
        }

        return copies.Count;
    }
}
