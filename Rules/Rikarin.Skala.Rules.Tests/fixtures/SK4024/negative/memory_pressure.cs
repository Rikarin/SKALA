using System;

sealed class MemoryPressureFixture {
    public void Announce() => GC.AddMemoryPressure(1024);
}
