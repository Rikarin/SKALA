// ⚠ `class voidAttribute` is legal C# and `[void]` is a parse error.
using System;

sealed class voidAttribute : Attribute { }

[voidAttribute]
class C { }
