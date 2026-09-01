using System;

sealed class WaitForFinalizersFixture {
    public void Drain() => GC.WaitForPendingFinalizers();
}
