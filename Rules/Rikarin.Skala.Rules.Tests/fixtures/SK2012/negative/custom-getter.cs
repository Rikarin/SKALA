class C { int value; int Value { get => ++value; set => this.value = value; } bool M() => Value == Value; }
