// An unconstrained `T` is not known to be a value type, so the operator may be doing real work.
namespace Fixtures {
    sealed class Passthrough {
        public static T Pass<T>(T? value) => value!;
    }
}
