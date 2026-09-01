interface I { void Increment(); } class C { void M<T>(ref T value) where T : struct, I { ((I)value).Increment(); } }
