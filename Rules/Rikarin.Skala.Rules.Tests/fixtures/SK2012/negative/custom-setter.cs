class C { int value; int Value { get => value; set { this.value = value; } } void M() { Value = Value; } }
