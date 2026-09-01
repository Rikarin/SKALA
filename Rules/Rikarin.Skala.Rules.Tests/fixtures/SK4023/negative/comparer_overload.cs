using System;
using System.Collections.Generic;

static class ComparerOverloadFixture {
    public static Dictionary<string, int> Make() => new Dictionary<string, int>(StringComparer.Ordinal);
}
