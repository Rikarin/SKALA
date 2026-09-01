interface I { void M(); } interface J : I { } class C { void M<T>(T value) where T : struct, J { ((I)value).M(); } }
