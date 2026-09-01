using System;

sealed class GenerationFixture {
    public void Trim() {
        GC.Collect(2);
    }
}
