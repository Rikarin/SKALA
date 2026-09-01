class C { int count; public int Count { get { System.Func<int> f = () => count; return f(); } set => count = value; } }
