using System.Linq;

public sealed class Buffer {
    readonly int[] values = new int[4];

    public int[] Values => values.ToArray();
}
