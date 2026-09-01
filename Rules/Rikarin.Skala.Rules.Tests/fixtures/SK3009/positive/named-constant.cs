using System; class C { const bool ThreadSafe = false; static Lazy<object> Value = new(isThreadSafe: ThreadSafe); }
