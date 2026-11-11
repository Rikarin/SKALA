// ⚠ Still a finding: the `exception` parameter is empty, so a sink gets a string where it
// expected an exception. The fix attaches the exception and deliberately does not also
// delete the now-redundant hole — removing text from inside a literal needs a value-to-
// source offset map that escape sequences break.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Reader {
        public void Run(Microsoft.Extensions.Logging.ILogger logger) {
            try {
                System.Console.WriteLine("work");
            } catch (System.IO.IOException ex) {
                logger.LogError("it failed: {Error}", ex);
            }
        }
    }
}
