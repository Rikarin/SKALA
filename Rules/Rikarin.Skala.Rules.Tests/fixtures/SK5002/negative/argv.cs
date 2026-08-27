using System.Diagnostics;

public static class Tool {
    public static void Main(string[] args) {
        Process.Start("dotnet", "build " + args[0]);
    }
}
