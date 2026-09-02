ref struct Wrapper {
    public int Value;
}

class C {
    public static void Take(scoped out Wrapper wrapper) => wrapper = default;
}
