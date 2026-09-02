ref struct Wrapper {
    public int Value;
}

class C {
    public static void Take(scoped ref Wrapper wrapper) => wrapper.Value = 1;
}
