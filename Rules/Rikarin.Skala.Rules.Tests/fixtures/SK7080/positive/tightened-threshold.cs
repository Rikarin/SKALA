// analyzer-option: dotnet_code_quality.SK7080.threshold = 2
// A repository that wants a flatter hierarchy tightens the threshold, and three bases now fire.
namespace Fixtures;

class Base { }

class Middle : Base { }

class Upper : Middle { }

class Leaf : Upper { }
