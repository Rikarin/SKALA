// The shape the rule converts *to*. This is also the fixture that would catch the fix looping: the
// analyzer must be silent on its own output.
namespace Fixtures {
    static class AlreadyConverted {
        extension(string value) {
            public bool IsBlank() => string.IsNullOrWhiteSpace(value);

            public string Repeat(int times) => new string('x', times) + value;
        }
    }
}
