using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public sealed class Registry {
    public static async Task Render(IAsyncEnumerable<int> numbers) {
        await foreach (var number in numbers) {
            if (number > 0) {
                System.Console.WriteLine(number);
            }
        }
    }
}
