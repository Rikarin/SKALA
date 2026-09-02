// `typeof(Helpers)` compiles and `value is Helpers` does not: CS0723, a static type in a pattern.
static class Helpers {
    public static int Zero => 0;
}

class StaticTarget {
    public bool Test(object value) => typeof(Helpers).IsInstanceOfType(value);
}
