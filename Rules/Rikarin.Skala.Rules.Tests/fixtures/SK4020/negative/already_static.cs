using System.Collections.Generic;
using System.Linq;

static class StaticLambdaFixture {
    public static IEnumerable<int> Lengths(IEnumerable<string> items) =>
        items.Select(static item => item.Length);
}
