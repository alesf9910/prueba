using System.Text;

namespace Prueba;

public sealed class LlvmEmitter
{
    private int _tempIndex;
    private int _labelIndex;

    public string EmitModule(Node.Program program)
    {
        _tempIndex = 0;
        _labelIndex = 0;
        var builder = new StringBuilder();
        builder.AppendLine("; ModuleID = 'prueba'");
        builder.AppendLine("declare i32 @putchar(i32)");
        builder.AppendLine("declare i64 @write(i32, i8*, i64)");
        builder.AppendLine("declare i8* @malloc(i64)");
        builder.AppendLine("declare void @free(i8*)");
        builder.AppendLine();

        foreach (var function in program.Functions)
        {
            EmitFunction(builder, function);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private void EmitFunction(StringBuilder builder, Node.Function function)
    {
        var returnType = MapType(function.ReturnType);
        builder.Append("define ").Append(returnType).Append(' ').Append('@').Append(function.Name).Append('(');
        for (var i = 0; i < function.Parameters.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            var param = function.Parameters[i];
            builder.Append(MapType(param.Type)).Append(' ').Append('%').Append(param.Name);
        }

        builder.AppendLine(") {");
        builder.AppendLine("entry:");

        var locals = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var param in function.Parameters)
        {
            var alloca = NextTemp();
            builder.AppendLine($"  {alloca} = alloca {MapType(param.Type)}");
            builder.AppendLine($"  store {MapType(param.Type)} %{param.Name}, {MapType(param.Type)}* {alloca}");
            locals[param.Name] = alloca;
        }

        EmitBlock(builder, function.Body, locals, function.ReturnType);

        if (function.ReturnType == TypeSymbol.Void)
        {
            builder.AppendLine("  ret void");
        }

        builder.AppendLine("}");
    }

    private void EmitBlock(StringBuilder builder, Node.Block block, Dictionary<string, string> locals, TypeSymbol returnType)
    {
        foreach (var statement in block.Statements)
        {
            EmitStatement(builder, statement, locals, returnType);
        }
    }

    private void EmitStatement(StringBuilder builder, Node.Statement statement, Dictionary<string, string> locals, TypeSymbol returnType)
    {
        switch (statement)
        {
            case Node.Statement.VariableDeclaration declaration:
                var alloca = NextTemp();
                builder.AppendLine($"  {alloca} = alloca {MapType(declaration.Type)}");
                locals[declaration.Name] = alloca;
                if (declaration.Initializer is not null)
                {
                    var value = EmitExpression(builder, declaration.Initializer, locals);
                    builder.AppendLine($"  store {MapType(declaration.Type)} {value}, {MapType(declaration.Type)}* {alloca}");
                }
                break;
            case Node.Statement.Assignment assignment:
                if (!locals.TryGetValue(assignment.Name, out var target))
                {
                    throw new InvalidOperationException($"Undefined variable '{assignment.Name}'.");
                }

                var assigned = EmitExpression(builder, assignment.Value, locals);
                builder.AppendLine($"  store {InferValueType(assignment.Value)} {assigned}, {InferValueType(assignment.Value)}* {target}");
                break;
            case Node.Statement.ExpressionStatement expressionStatement:
                EmitExpression(builder, expressionStatement.Expression, locals);
                break;
            case Node.Statement.Return ret:
                if (ret.Value is null)
                {
                    builder.AppendLine("  ret void");
                }
                else
                {
                    var value = EmitExpression(builder, ret.Value, locals);
                    builder.AppendLine($"  ret {MapType(returnType)} {value}");
                }
                break;
            case Node.Statement.If conditional:
                EmitIf(builder, conditional, locals, returnType);
                break;
            case Node.Statement.While loop:
                EmitWhile(builder, loop, locals, returnType);
                break;
        }
    }

    private void EmitIf(StringBuilder builder, Node.Statement.If conditional, Dictionary<string, string> locals, TypeSymbol returnType)
    {
        var conditionValue = EmitExpression(builder, conditional.Condition, locals);
        var thenLabel = NextLabel("then");
        var elseLabel = conditional.Else is null ? null : NextLabel("else");
        var mergeLabel = NextLabel("ifend");

        if (elseLabel is null)
        {
            builder.AppendLine($"  br i1 {conditionValue}, label %{thenLabel}, label %{mergeLabel}");
        }
        else
        {
            builder.AppendLine($"  br i1 {conditionValue}, label %{thenLabel}, label %{elseLabel}");
        }

        builder.AppendLine($"{thenLabel}:");
        EmitBlock(builder, conditional.Then, new Dictionary<string, string>(locals, StringComparer.Ordinal), returnType);
        builder.AppendLine($"  br label %{mergeLabel}");

        if (conditional.Else is not null)
        {
            builder.AppendLine($"{elseLabel}:");
            EmitBlock(builder, conditional.Else, new Dictionary<string, string>(locals, StringComparer.Ordinal), returnType);
            builder.AppendLine($"  br label %{mergeLabel}");
        }

        builder.AppendLine($"{mergeLabel}:");
    }

    private void EmitWhile(StringBuilder builder, Node.Statement.While loop, Dictionary<string, string> locals, TypeSymbol returnType)
    {
        var checkLabel = NextLabel("while_check");
        var bodyLabel = NextLabel("while_body");
        var endLabel = NextLabel("while_end");

        builder.AppendLine($"  br label %{checkLabel}");
        builder.AppendLine($"{checkLabel}:");
        var conditionValue = EmitExpression(builder, loop.Condition, locals);
        builder.AppendLine($"  br i1 {conditionValue}, label %{bodyLabel}, label %{endLabel}");
        builder.AppendLine($"{bodyLabel}:");
        EmitBlock(builder, loop.Body, new Dictionary<string, string>(locals, StringComparer.Ordinal), returnType);
        builder.AppendLine($"  br label %{checkLabel}");
        builder.AppendLine($"{endLabel}:");
    }

    private string EmitExpression(StringBuilder builder, Node.Expression expression, Dictionary<string, string> locals)
    {
        return expression switch
        {
            Node.Expression.Literal literal => EmitLiteral(literal),
            Node.Expression.Variable variable => EmitVariable(builder, variable, locals),
            Node.Expression.Unary unary => EmitUnary(builder, unary, locals),
            Node.Expression.Binary binary => EmitBinary(builder, binary, locals),
            Node.Expression.Call call => EmitCall(builder, call, locals),
            _ => "0"
        };
    }

    private string EmitLiteral(Node.Expression.Literal literal)
    {
        return literal.Value switch
        {
            bool value => value ? "1" : "0",
            long value => value.ToString(),
            double value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            _ => "0"
        };
    }

    private string EmitVariable(StringBuilder builder, Node.Expression.Variable variable, Dictionary<string, string> locals)
    {
        if (!locals.TryGetValue(variable.Name, out var alloca))
        {
            throw new InvalidOperationException($"Undefined variable '{variable.Name}'.");
        }

        var temp = NextTemp();
        var type = InferValueType(variable);
        builder.AppendLine($"  {temp} = load {type}, {type}* {alloca}");
        return temp;
    }

    private string EmitUnary(StringBuilder builder, Node.Expression.Unary unary, Dictionary<string, string> locals)
    {
        var operand = EmitExpression(builder, unary.Operand, locals);
        var type = InferValueType(unary.Operand);
        var temp = NextTemp();

        switch (unary.Operator)
        {
            case TokenKind.Minus when type == "double":
                builder.AppendLine($"  {temp} = fsub double 0.0, {operand}");
                break;
            case TokenKind.Minus:
                builder.AppendLine($"  {temp} = sub {type} 0, {operand}");
                break;
            case TokenKind.Bang:
                builder.AppendLine($"  {temp} = xor i1 {operand}, true");
                break;
        }

        return temp;
    }

    private string EmitBinary(StringBuilder builder, Node.Expression.Binary binary, Dictionary<string, string> locals)
    {
        var left = EmitExpression(builder, binary.Left, locals);
        var right = EmitExpression(builder, binary.Right, locals);
        var type = InferValueType(binary.Left);
        var temp = NextTemp();

        switch (binary.Operator)
        {
            case TokenKind.Plus when type == "double":
                builder.AppendLine($"  {temp} = fadd double {left}, {right}");
                break;
            case TokenKind.Minus when type == "double":
                builder.AppendLine($"  {temp} = fsub double {left}, {right}");
                break;
            case TokenKind.Star when type == "double":
                builder.AppendLine($"  {temp} = fmul double {left}, {right}");
                break;
            case TokenKind.Slash when type == "double":
                builder.AppendLine($"  {temp} = fdiv double {left}, {right}");
                break;
            case TokenKind.Plus:
                builder.AppendLine($"  {temp} = add {type} {left}, {right}");
                break;
            case TokenKind.Minus:
                builder.AppendLine($"  {temp} = sub {type} {left}, {right}");
                break;
            case TokenKind.Star:
                builder.AppendLine($"  {temp} = mul {type} {left}, {right}");
                break;
            case TokenKind.Slash:
                builder.AppendLine($"  {temp} = sdiv {type} {left}, {right}");
                break;
            case TokenKind.Percent:
                builder.AppendLine($"  {temp} = srem {type} {left}, {right}");
                break;
            case TokenKind.EqualEqual:
                builder.AppendLine($"  {temp} = icmp eq {type} {left}, {right}");
                break;
            case TokenKind.BangEqual:
                builder.AppendLine($"  {temp} = icmp ne {type} {left}, {right}");
                break;
            case TokenKind.Less:
                builder.AppendLine($"  {temp} = icmp slt {type} {left}, {right}");
                break;
            case TokenKind.LessEqual:
                builder.AppendLine($"  {temp} = icmp sle {type} {left}, {right}");
                break;
            case TokenKind.Greater:
                builder.AppendLine($"  {temp} = icmp sgt {type} {left}, {right}");
                break;
            case TokenKind.GreaterEqual:
                builder.AppendLine($"  {temp} = icmp sge {type} {left}, {right}");
                break;
            case TokenKind.AndAnd:
                builder.AppendLine($"  {temp} = and i1 {left}, {right}");
                break;
            case TokenKind.OrOr:
                builder.AppendLine($"  {temp} = or i1 {left}, {right}");
                break;
        }

        return temp;
    }

    private string EmitCall(StringBuilder builder, Node.Expression.Call call, Dictionary<string, string> locals)
    {
        var args = new List<string>();
        foreach (var argument in call.Arguments)
        {
            var value = EmitExpression(builder, argument, locals);
            args.Add($"{InferValueType(argument)} {value}");
        }

        var temp = NextTemp();
        builder.AppendLine($"  {temp} = call i64 @{call.Callee}({string.Join(", ", args)})");
        return temp;
    }

    private string MapType(TypeSymbol type)
    {
        return type switch
        {
            TypeSymbol.I32 => "i32",
            TypeSymbol.I64 => "i64",
            TypeSymbol.F64 => "double",
            TypeSymbol.Bool => "i1",
            TypeSymbol.Ptr => "i8*",
            TypeSymbol.Void => "void",
            _ => "i64"
        };
    }

    private string InferValueType(Node.Expression expression)
    {
        return expression switch
        {
            Node.Expression.Literal literal => MapType(literal.Type),
            Node.Expression.Variable => "i64",
            Node.Expression.Unary unary => InferValueType(unary.Operand),
            Node.Expression.Binary binary => InferValueType(binary.Left),
            Node.Expression.Call => "i64",
            _ => "i64"
        };
    }

    private string NextTemp() => $"%t{_tempIndex++}";

    private string NextLabel(string prefix) => $"{prefix}_{_labelIndex++}";
}
