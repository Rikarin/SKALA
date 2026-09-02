ref struct Wrapper {
    public int Value;
}

class C {
    public static int Take(scoped Wrapper wrapper) => wrapper.Value;
}
