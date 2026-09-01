using System;

sealed class MethodGroupFixture {
    public Action Deferred() => GC.Collect;
}
