using System.Linq;

public sealed class Registry {
    // ⚠ `entries.ElementAt(2)` throws ArgumentOutOfRangeException and `entries[2]` throws
    // IndexOutOfRangeException, so a catch written for one stops catching.
    public static int Third(int[] entries) => entries.ElementAt(2);
}
