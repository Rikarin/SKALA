public abstract class Failure {
    public abstract string Describe();
}

public sealed class TimedOut : Failure {
    public override string Describe() => "timed out";
}

public sealed class Cancelled : Failure {
    public override string Describe() => "cancelled";
}

public sealed class Unknown : Failure {
    public override string Describe() => "unknown";
}
