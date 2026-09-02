// Reused as scratch after its value has been used, which is untidy and not a defect: the
// caller's argument did the job it was passed for.
namespace Fixtures {
    sealed class Reusing {
        public void Render(string path) {
            System.Console.WriteLine(path);
            path = "default.txt";
            System.Console.WriteLine(path);
        }
    }
}
