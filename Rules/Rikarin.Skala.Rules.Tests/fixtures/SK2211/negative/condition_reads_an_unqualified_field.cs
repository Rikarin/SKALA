// ⚠ This fixture exists because *three* guards were covering for each other and none could be shown
// to work on its own. A condition reading only a field is declined by the `MemberAccessExpression`
// case when it is written `this.stopped`, by the `IFieldSymbol` case when it is written `stopped`,
// and — when both are removed — by the "no variables at all" check, because a walk that collects
// nothing has nothing to prove. Each sabotage left every fixture green and each guard read as
// redundant.
//
// The shape that isolates the identifier switch is a condition mixing a local with a field. `i` is
// collected and is never written, so the variable list is not empty and the empty-list check cannot
// decline it; there is no member access, so that case cannot either. `limit` binding to an
// `IFieldSymbol` is the only thing left, and it is the thing under test — another method may raise
// `limit` while this loop spins, which is exactly the reasoning the rule must not skip.
class C {
    int limit;

    void Spin() {
        var i = 0;
        while (i < this.limit) {
            System.Threading.Thread.Sleep(1);
        }
    }

    void SpinUnqualified() {
        var i = 0;
        while (i < limit) {
            System.Threading.Thread.Sleep(1);
        }
    }
}
