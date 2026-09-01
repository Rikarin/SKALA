using System; class C { void M(int value) { for (Action a = () => Console.WriteLine(value); value > 0; value--) a(); } }
