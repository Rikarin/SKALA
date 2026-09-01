// Exactly four source-declared bases. The family reports `> threshold`, so the threshold itself
// is silent — this is the fixture that proves the boundary is not off by one.
namespace Fixtures;

class Level1 { }

class Level2 : Level1 { }

class Level3 : Level2 { }

class Level4 : Level3 { }

class AtTheThreshold : Level4 { }
