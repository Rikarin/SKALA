namespace Contoso.Design;

public sealed class Session {
    private Session(string token) => Token = token;

    public string Token { get; }

    public int Length => Token.Length;
}
