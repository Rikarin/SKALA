// Two findings, two non-overlapping edits: each takes the comma in front of it.
using System;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
sealed class MarkerAttribute : Attribute { }
