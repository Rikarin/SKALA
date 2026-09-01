// An interface has a graph of bases rather than a chain, and a class implementing a long
// interface hierarchy inherits no implementation to trace. Neither is measured.
namespace Fixtures;

interface IA { }

interface IB : IA { }

interface IC : IB { }

interface ID : IC { }

interface IE : ID { }

interface IF : IE { }

class Implements : IF { }
