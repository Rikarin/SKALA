using System.Collections.Generic;
using System.Linq;

static class LambdaFixture {
    public static IEnumerable<int> Lengths(IEnumerable<string> items) => items.Select(item => item.Length);
}
