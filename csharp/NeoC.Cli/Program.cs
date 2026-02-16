using NeoC.Core;

// CLI de NeoC: flujo ingeniería lexer -> parser -> sema -> LLVM -> nativo.
if (args.Length < 1)
{
    Console.Error.WriteLine("Uso: neoc <archivo.neoc> [nombre_salida]");
    Environment.Exit(1);
}

try
{
    var input = args[0];
    var outName = args.Length > 1 ? args[1] : "programa";
    var source = File.ReadAllText(input);

    var lexer = new Lexer(source);
    var tokens = lexer.Tokenize();
    var parser = new Parser(tokens);
    var ast = parser.ParseProgram();

    SemanticAnalyzer.Check(ast);
    var output = LlvmBackend.Build(ast, "build", outName);

    Console.WriteLine("OK: compilación completada");
    Console.WriteLine($"- IR: {output.IrPath}");
    Console.WriteLine($"- OBJ: {output.ObjPath}");
    Console.WriteLine($"- BIN: {output.BinPath}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    Environment.Exit(1);
}
