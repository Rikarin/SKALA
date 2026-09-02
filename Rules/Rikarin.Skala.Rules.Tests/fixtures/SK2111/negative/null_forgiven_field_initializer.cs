// `null!` is the idiom for a field a framework assigns later. The operand's type is the null
// literal's, which is no type at all, and the suppression is doing exactly what it claims.
namespace Fixtures {
    sealed class Injected {
        public string Name = null!;
    }
}
