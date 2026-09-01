namespace Contoso.Design;

public abstract class Report {
    public string Title { get; init; } = string.Empty;

    public int Render() => Title.Length;
}
