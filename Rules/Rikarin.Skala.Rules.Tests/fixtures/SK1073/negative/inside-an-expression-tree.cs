// ⚠ #425: in an expression tree the creation is a `New` node and the cached member a `MemberAccess`
// node, and a query provider translates the two differently. Measured for #412's audit: `New` before
// the fix and `MemberAccess` after it.
using System;
using System.Linq.Expressions;

public static class Probe {
    public static string Run() {
        Expression<Func<Guid>> empty = () => new Guid();
        return empty.Body.NodeType.ToString();
    }
}
