using System.Text;

sealed class Formatter {
    StringBuilder Build() => new();

    public string Render() => Build().ToString();
}
