using System;

sealed class KeepAliveFixture {
    public void Hold(object value) => GC.KeepAlive(value);
}
