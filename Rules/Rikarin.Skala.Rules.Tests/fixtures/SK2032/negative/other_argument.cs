using System;

sealed class Pool {
    public void Release(object owned) {
        GC.SuppressFinalize(owned);
    }
}
