// ⚠ A local function at the file's level pays `blank_lines_around_local_method` exactly as one in a body
// does. A top-level statement is wrapped in a `GlobalStatementSyntax`, which is a member declaration, and
// as the outermost node it hid the function from the requirement: a comment glued above makes the function
// multi-line for the gap above the comment, and a multi-line function takes a blank line on both sides.
// Issue #498.
System.Console.WriteLine();
/* s3 */ static void First() { }
System.Console.WriteLine();
// s3
static void Second() { }
static void Third() { }
// s3
static void Fourth() { }
System.Console.WriteLine();
static void Fifth() {
    System.Console.WriteLine();
}
System.Console.WriteLine();
// d
System.Console.WriteLine();
static void Sixth() { }
System.Console.WriteLine();
