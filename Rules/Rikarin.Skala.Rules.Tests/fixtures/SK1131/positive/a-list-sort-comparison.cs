using System.Collections.Generic;

public static class Ranking {
    public static void Order(List<int> ranks) {
        ranks.Sort(delegate(int a, int b) { return a.CompareTo(b); });
    }
}
