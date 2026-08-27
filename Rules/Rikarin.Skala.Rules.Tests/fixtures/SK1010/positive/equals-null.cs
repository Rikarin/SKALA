using System.Collections.Generic;

public sealed class Holder {
    public static bool Empty(List<int> items) {
        if (items == null) {
            return true;
        }

        return false;
    }
}
