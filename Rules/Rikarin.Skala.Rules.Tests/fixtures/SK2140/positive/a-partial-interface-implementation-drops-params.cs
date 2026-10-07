// #400: the interface-implementation `params` disagreement on a partial method. Both halves must
// agree about `params` (CS0758), so the fix adds it to both; `PartialMemberTests` asserts the
// round trip compiles.
namespace Fixtures {
    interface IPlain {
        void Accept(string name, params int[] values);
    }

    sealed partial class PlainSink : IPlain {
        public partial void Accept(string name, int[] values);

        public partial void Accept(string name, int[] values) { }
    }
}
