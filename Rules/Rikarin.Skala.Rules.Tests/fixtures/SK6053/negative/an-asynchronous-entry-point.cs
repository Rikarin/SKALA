using System.Threading.Tasks;

namespace Contoso.Design;

// `Main` is spelled by the language. `MainAsync` is not an entry point, so the suffix cannot be added.
public static class Program {
    public static async Task Main(string[] args) {
        await Task.Yield();
    }
}
