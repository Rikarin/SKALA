using System.Collections.Generic;

static class ImplicitCapacityFixture {
    public static HashSet<int> Make() {
        HashSet<int> set = new(0);
        return set;
    }
}
