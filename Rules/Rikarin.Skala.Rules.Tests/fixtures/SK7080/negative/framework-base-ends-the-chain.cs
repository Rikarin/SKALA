// ⚠ The load-bearing exclusion. `ArgumentOutOfRangeException` sits three levels above `Exception`
// in the framework, so the raw depth of `Leaf` here is seven. Only the four this compilation
// declares are counted, the framework base contributes one and stops the walk, and nothing fires.
// Counting the other three would make the rule an opinion about which base class the framework
// offers, which nobody in a repository can act on.
namespace Fixtures;

class DomainError : System.ArgumentOutOfRangeException { }

class ParseError : DomainError { }

class SyntaxError : ParseError { }

class Leaf : SyntaxError { }
