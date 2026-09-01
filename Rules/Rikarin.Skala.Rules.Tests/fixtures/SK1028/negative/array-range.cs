using System.Text; class C { string M(byte[] bytes) => Encoding.UTF8.GetString(bytes[1..]); }
