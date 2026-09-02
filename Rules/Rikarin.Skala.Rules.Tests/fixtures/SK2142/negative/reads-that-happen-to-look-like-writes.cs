// `+=`, `++` and `??=` all read before they write, so the incoming value flows in and none of
// them is a finding. A rule matching "the parameter appears on the left of an assignment" would
// report all four of these members.
namespace Fixtures {
    sealed class Normalising {
        public void Compound(int count) {
            count += 1;
            System.Console.WriteLine(count);
        }

        public void Increment(int count) {
            count++;
            System.Console.WriteLine(count);
        }

        public void Coalesce(string? name) {
            name ??= "anonymous";
            System.Console.WriteLine(name);
        }

        public void Trim(string path) {
            path = path.Trim();
            System.Console.WriteLine(path);
        }
    }
}
