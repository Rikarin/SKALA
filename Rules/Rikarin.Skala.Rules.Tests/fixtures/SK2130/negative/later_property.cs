// A static property is a method that runs when it is called, so it reads the field's value at
// access time rather than at initializer time. Not an ordering defect.
static class Config {
    public static readonly int Doubled = Later * 2;

    public static int Later => 21;
}
