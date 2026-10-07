using Xunit;

// The constant reaches `Equal<long>` through the language's own widening, which runs nothing, so the
// swap moves only which name the failure message gives each value.
public static class Probe {
    static long Measure() => 5;

    public static string Run() {
        Assert.Equal(Measure(), 5);
        return "equal";
    }
}
