// ⚠ `[Marker]` searches for both `Marker` and `MarkerAttribute`. With a `Marker` in scope the
// shortened form no longer names the same thing, so the suffix is not redundant.
using System;

sealed class Marker { }

sealed class MarkerAttribute : Attribute { }

[MarkerAttribute]
class C { }
