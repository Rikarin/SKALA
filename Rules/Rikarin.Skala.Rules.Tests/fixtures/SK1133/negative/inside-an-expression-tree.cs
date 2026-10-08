// fixture-option: LangVersion = 14
// CS9175: an expression tree holds no collection expression. The argument is a written target, so
// only the expression-tree guard declines it.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

public static class Trees {
    static int Count(int[] values) => values.Length;

    public static Expression<Func<IEnumerable<int>, int>> Counter() => source => Count(source.ToArray());
}
