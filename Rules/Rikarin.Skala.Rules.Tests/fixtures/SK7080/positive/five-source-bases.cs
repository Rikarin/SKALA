// Five source-declared base classes above `Deepest`, over the default threshold of 4.
namespace Fixtures;

class Level1 { }

class Level2 : Level1 { }

class Level3 : Level2 { }

class Level4 : Level3 { }

class Level5 : Level4 { }

class Deepest : Level5 { }
