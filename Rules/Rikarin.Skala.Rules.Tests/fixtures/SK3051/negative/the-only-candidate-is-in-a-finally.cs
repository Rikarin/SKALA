using System;
using System.Threading.Tasks;

// ⚠ SK3004 declines to forward a token into cleanup, so a call there is not evidence: reporting
// the method would leave a finding whose only repair the sibling rule then refuses to make.
public sealed class Worker {
    public async Task RunAsync() {
        try {
            Console.WriteLine("work");
        } finally {
            await Task.Delay(5);
        }
    }
}
