using System;

sealed class TotalMemoryFixture {
    public long Read() => GC.GetTotalMemory(false);
}
