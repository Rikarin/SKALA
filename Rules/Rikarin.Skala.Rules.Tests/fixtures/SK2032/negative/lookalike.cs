sealed class Local {
    public void Dispose() {
        GC.SuppressFinalize(this);
    }

    static class GC {
        public static void SuppressFinalize(object value) { }
    }
}
