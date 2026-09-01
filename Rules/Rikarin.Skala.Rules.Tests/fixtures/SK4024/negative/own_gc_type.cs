static class OwnGcFixture {
    public static void Refresh() => GC.Collect();
}

static class GC {
    public static void Collect() { }
}
