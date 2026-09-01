sealed class Session {
    public string User { get; set; } = "";

    public override bool Equals(object? other) => other is Session session && session.User == User;

    public override int GetHashCode() => User.GetHashCode();
}
