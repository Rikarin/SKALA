using System;

sealed class RefreshFixture {
    public void Refresh() {
        GC.Collect();
    }
}
