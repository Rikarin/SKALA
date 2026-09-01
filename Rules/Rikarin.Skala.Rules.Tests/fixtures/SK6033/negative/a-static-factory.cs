namespace Contoso.Design;

public sealed class Session {
    private Session(string token) => Token = token;

    public string Token { get; }

    public static Session Open(string token) => new(token);
}
