using System.Collections.Generic;
using System.Linq;

public sealed class Bucket {
    public readonly List<int> Items = new List<int>();

    // ⚠ `source.Items` and `target.Items` are one field symbol and two lists. Symbol equality alone
    // would report this, which is why the receiver's text has to match as well.
    public static void Move(Bucket source, Bucket target) {
        foreach (var item in source.Items) {
            target.Items.Add(item);
        }
    }
}
