public abstract record Failure(string Message);

public sealed record TimeoutException(string Message) : Failure(Message);
