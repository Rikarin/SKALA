using System.Collections.Generic;
using System.Linq;

// ⚠ #329 defect 2: `used.Add(…)` mutates a `HashSet`. The rewrite is behaviourally identical today,
// because `Where` is lazy and runs the predicate once per element in order — and it puts a side
// effect in a filter, where a later `.ToList()` or a second enumeration changes the program with
// nothing in the diff to say so.
public sealed class Keys {
    public static void Collisions(IEnumerable<string> keys, List<string> collisions) {
        var used = new HashSet<string>();
        foreach (var key in keys) {
            if (!used.Add(key)) {
                collisions.Add(key);
            }
        }
    }
}
