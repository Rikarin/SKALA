public sealed class Session {
    public int Id { get; set; }
}

public static class Sessions {
    public static Session Start() {
        Session session = new() { };
        return session;
    }
}
