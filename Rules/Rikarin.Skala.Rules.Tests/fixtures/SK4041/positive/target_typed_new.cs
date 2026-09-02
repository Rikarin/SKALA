using System.Text;

public sealed class Report {
    public int Write(string name) {
        StringBuilder builder = new();
        builder.Append(name);
        builder.Replace('a', 'b');
        return name.Length;
    }
}
