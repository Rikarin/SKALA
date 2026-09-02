namespace Fixture {
    // A source type of the same name is never matched.
    public struct DateTime {
        public DateTime(int year, int month, int day) { }

        public DateTime ToUniversalTime() => this;
    }

    public sealed class Schedule {
        public DateTime Starts() => new DateTime(2026, 1, 2).ToUniversalTime();
    }
}
