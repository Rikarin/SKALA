sealed class Session {
    public Session(string user) => User = user;

    public string User { get; }

    public override bool Equals(object? other) => other is Session session && session.User == User;

    public override int GetHashCode() => User.GetHashCode();
}
