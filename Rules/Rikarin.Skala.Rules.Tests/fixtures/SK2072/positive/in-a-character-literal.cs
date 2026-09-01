// A byte order mark as a char constant, which compares equal to nothing anybody typed.
// contains: U+FEFF
namespace Fixtures;

sealed class Marks {
    public const char Leading = '﻿';
}
