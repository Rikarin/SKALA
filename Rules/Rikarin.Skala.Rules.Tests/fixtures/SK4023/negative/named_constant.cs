using System.Collections.Generic;

static class NamedConstantFixture {
    const int Capacity = 0;

    public static List<int> Make() => new List<int>(Capacity);
}
