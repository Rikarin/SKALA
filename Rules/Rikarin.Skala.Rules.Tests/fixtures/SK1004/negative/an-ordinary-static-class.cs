// No `this` parameter anywhere: a static class of plain helpers is not the idiom being modernized.
namespace Fixtures {
    static class PlainHelpers {
        public static string Join(string a, string b) => a + b;

        public static int Size(string a) => a.Length;
    }
}
