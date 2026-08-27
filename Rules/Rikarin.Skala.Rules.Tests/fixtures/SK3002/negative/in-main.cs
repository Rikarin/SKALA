using System.IO;
using System.Threading.Tasks;

public static class Program {
    public static void Main(string[] args) {
        // A synchronous entry point has nowhere to await from and no context to deadlock against.
        var text = File.ReadAllTextAsync(args[0]).Result;
        System.Console.WriteLine(text.Length);
    }
}
