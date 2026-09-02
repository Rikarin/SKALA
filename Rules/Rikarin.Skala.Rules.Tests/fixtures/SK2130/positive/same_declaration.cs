// Two declarators in one declaration are still two initializers, run in the order written.
static class Limits {
    static readonly int Half = Total / 2, Total = 100;

    public static int Value => Half + Total;
}
