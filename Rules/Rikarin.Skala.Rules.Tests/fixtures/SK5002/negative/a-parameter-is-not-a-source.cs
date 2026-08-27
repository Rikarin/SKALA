using System.Diagnostics;

public static class Runner {
    public static void Run(string program, string arguments) {
        Process.Start(program, arguments);
    }
}
