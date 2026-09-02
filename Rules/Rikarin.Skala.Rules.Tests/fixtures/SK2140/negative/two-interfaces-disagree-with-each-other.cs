// ⚠ The guard for "which base?". One member implements two interface members that declare
// different defaults, so there is no single value to align to and a fix would have to pick one.
// Declined outright rather than reported against whichever interface was enumerated first.
//
// ⚠ The declared value disagrees with *both* interfaces on purpose. Written to agree with one of
// them, this fixture would pass whenever the enumeration happened to reach that one first, and
// deleting the guard would leave it green — which is the shape of a test that proves nothing.
namespace Fixtures {
    interface IFast {
        void Run(string name, int level = 0);
    }

    interface ISlow {
        void Run(string name, int level = 1);
    }

    sealed class Runner : IFast, ISlow {
        public void Run(string name, int level = 2) { }
    }
}
