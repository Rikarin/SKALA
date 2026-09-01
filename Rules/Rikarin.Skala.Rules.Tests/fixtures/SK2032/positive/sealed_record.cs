sealed record Handle(int Value) : System.IDisposable {
    public void Dispose() {
        System.GC.SuppressFinalize(this);
    }
}
