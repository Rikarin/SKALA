sealed class Writer : System.IDisposable {
    public void Dispose() {
        System.GC.SuppressFinalize(this);
    }
}
