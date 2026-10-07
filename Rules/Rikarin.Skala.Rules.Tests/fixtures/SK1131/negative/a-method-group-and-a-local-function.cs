using System;
using System.Collections.Generic;

public static class Groups {
    static int Compare(int a, int b) => a.CompareTo(b);

    public static void Order(List<int> values) {
        values.Sort(Compare);

        int Reverse(int a, int b) => b.CompareTo(a);

        values.Sort(Reverse);
    }
}
