using System.Text;

public sealed class Report {
    // ⚠ Nothing was filled, so nothing was thrown away. An unused local is a different finding with
    // a different answer.
    public int Write(string name) {
        var builder = new StringBuilder();
        return name.Length;
    }
}
