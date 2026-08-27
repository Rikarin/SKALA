public sealed class FactAttribute : System.Attribute { }

public sealed class Clock {
    public static void Sleep(int milliseconds) { }
}

public sealed class ClockTests {
    [Fact]
    public void Advances() {
        Clock.Sleep(200);
    }
}
