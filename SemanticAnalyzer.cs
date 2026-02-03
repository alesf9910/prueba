namespace Prueba;

public sealed class SemanticAnalyzer
{
    private readonly Dictionary<string, FunctionSymbol> _functions = new(StringComparer.Ordinal);
    private readonly Stack<Dictionary<string, TypeSymbol>> _scopes = new();

    public IReadOnlyList<string> Analyze(Node.Program program)
    {
        _functions.Clear();
        _scopes.Clear();
        var diagnostics = new List<string>();

        foreach (var builtin in StdLib.Functions)
        {
            _functions[builtin.Name] = new FunctionSymbol(
                builtin.Name,
                builtin.Parameters.Select(parameter => new Node.Parameter(parameter.Name, parameter.Type)).ToList(),
                builtin.ReturnType);
        }

        foreach (var function in program.Functions)
        {
            if (_functions.ContainsKey(function.Name))
            {
                diagnostics.Add($"Function '{function.Name}' is already defined.");
                continue;
            }

            _functions.Add(function.Name, new FunctionSymbol(function.Name, function.Parameters, function.ReturnType));
        }

        foreach (var function in program.Functions)
        {
            AnalyzeFunction(function, diagnostics);
        }

        return diagnostics;
    }

    private void AnalyzeFunction(Node.Function function, List<string> diagnostics)
    {
        _scopes.Push(new Dictionary<string, TypeSymbol>(StringComparer.Ordinal));
        foreach (var parameter in function.Parameters)
        {
            if (!_scopes.Peek().TryAdd(parameter.Name, parameter.Type))
            {
                diagnostics.Add($"Parameter '{parameter.Name}' is already defined in function '{function.Name}'.");
            }
        }

        AnalyzeBlock(function.Body, function.ReturnType, diagnostics);
        _scopes.Pop();
    }

    private void AnalyzeBlock(Node.Block block, TypeSymbol returnType, List<string> diagnostics)
    {
        _scopes.Push(new Dictionary<string, TypeSymbol>(_scopes.Peek(), StringComparer.Ordinal));
        foreach (var statement in block.Statements)
        {
            AnalyzeStatement(statement, returnType, diagnostics);
        }

        _scopes.Pop();
    }

    private void AnalyzeStatement(Node.Statement statement, TypeSymbol returnType, List<string> diagnostics)
    {
        switch (statement)
        {
            case Node.Statement.VariableDeclaration declaration:
                if (!_scopes.Peek().TryAdd(declaration.Name, declaration.Type))
                {
                    diagnostics.Add($"Variable '{declaration.Name}' is already defined in this scope.");
                }

                if (declaration.Initializer is not null)
                {
                    var initType = AnalyzeExpression(declaration.Initializer, diagnostics);
                    if (initType != declaration.Type)
                    {
                        diagnostics.Add($"Cannot assign '{initType}' to '{declaration.Type}' for '{declaration.Name}'.");
                    }
                }
                break;
            case Node.Statement.Assignment assignment:
                if (!TryResolveVariable(assignment.Name, out var variableType))
                {
                    diagnostics.Add($"Undefined variable '{assignment.Name}'.");
                    break;
                }

                var valueType = AnalyzeExpression(assignment.Value, diagnostics);
                if (valueType != variableType)
                {
                    diagnostics.Add($"Cannot assign '{valueType}' to '{variableType}' for '{assignment.Name}'.");
                }
                break;
            case Node.Statement.Return ret:
                if (ret.Value is null)
                {
                    if (returnType != TypeSymbol.Void)
                    {
                        diagnostics.Add("Return statement missing value.");
                    }
                }
                else
                {
                    var retType = AnalyzeExpression(ret.Value, diagnostics);
                    if (retType != returnType)
                    {
                        diagnostics.Add($"Return type '{retType}' does not match '{returnType}'.");
                    }
                }
                break;
            case Node.Statement.ExpressionStatement expr:
                AnalyzeExpression(expr.Expression, diagnostics);
                break;
            case Node.Statement.If conditional:
                var conditionType = AnalyzeExpression(conditional.Condition, diagnostics);
                if (conditionType != TypeSymbol.Bool)
                {
                    diagnostics.Add("If condition must be bool.");
                }

                AnalyzeBlock(conditional.Then, returnType, diagnostics);
                if (conditional.Else is not null)
                {
                    AnalyzeBlock(conditional.Else, returnType, diagnostics);
                }
                break;
            case Node.Statement.While loop:
                var loopType = AnalyzeExpression(loop.Condition, diagnostics);
                if (loopType != TypeSymbol.Bool)
                {
                    diagnostics.Add("While condition must be bool.");
                }

                AnalyzeBlock(loop.Body, returnType, diagnostics);
                break;
        }
    }

    private TypeSymbol AnalyzeExpression(Node.Expression expression, List<string> diagnostics)
    {
        return expression switch
        {
            Node.Expression.Literal literal => literal.Type,
            Node.Expression.Variable variable => ResolveVariable(variable.Name, diagnostics),
            Node.Expression.Unary unary => AnalyzeUnary(unary, diagnostics),
            Node.Expression.Binary binary => AnalyzeBinary(binary, diagnostics),
            Node.Expression.Call call => AnalyzeCall(call, diagnostics),
            _ => TypeSymbol.Void
        };
    }

    private TypeSymbol AnalyzeUnary(Node.Expression.Unary unary, List<string> diagnostics)
    {
        var operandType = AnalyzeExpression(unary.Operand, diagnostics);
        return unary.Operator switch
        {
            TokenKind.Bang when operandType == TypeSymbol.Bool => TypeSymbol.Bool,
            TokenKind.Minus when operandType is TypeSymbol.I32 or TypeSymbol.I64 or TypeSymbol.F64 => operandType,
            _ => ReportUnaryError(unary.Operator, operandType, diagnostics)
        };
    }

    private TypeSymbol AnalyzeBinary(Node.Expression.Binary binary, List<string> diagnostics)
    {
        var left = AnalyzeExpression(binary.Left, diagnostics);
        var right = AnalyzeExpression(binary.Right, diagnostics);

        if (binary.Operator is TokenKind.AndAnd or TokenKind.OrOr)
        {
            if (left == TypeSymbol.Bool && right == TypeSymbol.Bool)
            {
                return TypeSymbol.Bool;
            }

            diagnostics.Add("Logical operators require bool operands.");
            return TypeSymbol.Bool;
        }

        if (binary.Operator is TokenKind.EqualEqual or TokenKind.BangEqual)
        {
            if (left != right)
            {
                diagnostics.Add("Equality operators require operands of the same type.");
            }

            return TypeSymbol.Bool;
        }

        if (binary.Operator is TokenKind.Less or TokenKind.LessEqual or TokenKind.Greater or TokenKind.GreaterEqual)
        {
            if (left != right)
            {
                diagnostics.Add("Comparison operators require operands of the same type.");
            }

            return TypeSymbol.Bool;
        }

        if (left != right)
        {
            diagnostics.Add("Binary operators require operands of the same type.");
        }

        return left;
    }

    private TypeSymbol AnalyzeCall(Node.Expression.Call call, List<string> diagnostics)
    {
        if (!_functions.TryGetValue(call.Callee, out var function))
        {
            diagnostics.Add($"Undefined function '{call.Callee}'.");
            foreach (var argument in call.Arguments)
            {
                AnalyzeExpression(argument, diagnostics);
            }

            return TypeSymbol.Void;
        }

        if (function.Parameters.Count != call.Arguments.Count)
        {
            diagnostics.Add($"Function '{call.Callee}' expects {function.Parameters.Count} arguments but got {call.Arguments.Count}.");
        }

        for (var i = 0; i < Math.Min(function.Parameters.Count, call.Arguments.Count); i++)
        {
            var argType = AnalyzeExpression(call.Arguments[i], diagnostics);
            if (argType != function.Parameters[i].Type)
            {
                diagnostics.Add($"Argument {i + 1} of '{call.Callee}' expects '{function.Parameters[i].Type}' but got '{argType}'.");
            }
        }

        return function.ReturnType;
    }

    private TypeSymbol ResolveVariable(string name, List<string> diagnostics)
    {
        if (TryResolveVariable(name, out var type))
        {
            return type;
        }

        diagnostics.Add($"Undefined variable '{name}'.");
        return TypeSymbol.Void;
    }

    private bool TryResolveVariable(string name, out TypeSymbol type)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetValue(name, out type))
            {
                return true;
            }
        }

        type = TypeSymbol.Void;
        return false;
    }

    private static TypeSymbol ReportUnaryError(TokenKind oper, TypeSymbol operandType, List<string> diagnostics)
    {
        diagnostics.Add($"Unary operator '{oper}' is not valid for '{operandType}'.");
        return operandType;
    }

    private sealed record FunctionSymbol(string Name, IReadOnlyList<Node.Parameter> Parameters, TypeSymbol ReturnType);
}
