static class NestedGcFixture {
    public static void Refresh() => Host.GC.Collect();
}

static class Host {
    public static class GC {
        public static void Collect() { }
    }
}
