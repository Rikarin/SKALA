using System;

sealed class ForcedModeFixture {
    public void Compact() {
        GC.Collect(2, GCCollectionMode.Forced, true);
    }
}
