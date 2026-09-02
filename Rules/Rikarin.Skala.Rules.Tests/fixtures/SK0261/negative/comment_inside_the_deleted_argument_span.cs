using System;

[AttributeUsage(AttributeTargets.Class, /* pinned deliberately */ Inherited = true)]
sealed class MarkerAttribute : Attribute { }
