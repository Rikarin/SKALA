using System;

sealed class ExpressionBodyFixture {
    public void Purge() => GC.Collect();
}
