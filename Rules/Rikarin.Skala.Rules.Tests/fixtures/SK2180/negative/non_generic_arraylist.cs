using System.Collections;

static class Legacy {
    // An `ArrayList` yields `object` and offers no other spelling: the cast is the API's doing.
    public static int Length(ArrayList names) {
        var total = 0;
        foreach (string name in names) {
            total += name.Length;
        }

        return total;
    }
}
