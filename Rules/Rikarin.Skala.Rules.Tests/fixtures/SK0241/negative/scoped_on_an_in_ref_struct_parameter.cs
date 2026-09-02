ref struct Wrapper {
    public int Value;
}

class C {
    public static int Take(scoped in Wrapper wrapper) => wrapper.Value;
}
