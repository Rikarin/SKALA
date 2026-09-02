// Every caller computes a path, passes it, and has it discarded on the first line. Nothing at
// the call site can show that, which is why this is worth a warning rather than a hint.
namespace Fixtures {
    sealed class Renderer {
        public void Render(string path) {
            path = "default.txt";
            System.Console.WriteLine(path);
        }
    }
}
