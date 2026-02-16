namespace NeoC.Core;

public static partial struct SemanticAnalyzer
{
    public static void Check(ProgramNode program)
    {
        var functions = new Dictionary<string, (List<TypeRef> Params, TypeRef Ret)>();
        var structs = new HashSet<string>();

        foreach (var item in program.Items)
        {
            if (item.Kind == ItemKind.Struct)
            {
                var s = item.Struct;
                if (!structs.Add(s.Name)) throw new Exception($"Struct duplicada: {s.Name}");
                continue;
            }

            var f = item.Function;
            if (functions.ContainsKey(f.Name)) throw new Exception($"Función duplicada: {f.Name}");
            functions[f.Name] = (f.Parameters.Select(p => p.Type).ToList(), f.ReturnType);
        }

        foreach (var item in program.Items)
        {
            if (item.Kind != ItemKind.Function || item.Function.Body is null) continue;
            var fn = item.Function;
            var locals = fn.Parameters.ToDictionary(p => p.Name, p => p.Type);
            foreach (var stmt in fn.Body) CheckStmt(stmt, fn.ReturnType, locals, functions);
        }
    }

    private static void CheckStmt(
        StmtNode stmt,
        TypeRef returnType,
        Dictionary<string, TypeRef> locals,
        Dictionary<string, (List<TypeRef> Params, TypeRef Ret)> functions)
    {
        switch (stmt.Kind)
        {
            case StmtKind.Let:
            {
                var s = stmt.Let;
                var exprType = InferExpr(s.Value, locals, functions);
                if (exprType != s.Type) throw new Exception($"Tipo incompatible en let {s.Name}");
                locals[s.Name] = s.Type;
                break;
            }
            case StmtKind.Return:
            {
                var s = stmt.Return;
                if (InferExpr(s.Value, locals, functions) != returnType)
                    throw new Exception("Tipo de return incorrecto");
                break;
            }
            case StmtKind.Expr:
                _ = InferExpr(stmt.Expr.Value, locals, functions);
                break;
        }
    }

    private static TypeRef InferExpr(
        ExprNode expr,
        Dictionary<string, TypeRef> locals,
        Dictionary<string, (List<TypeRef> Params, TypeRef Ret)> functions)
    {
        return expr.Kind switch
        {
            ExprKind.Int => TypeRef.Int,
            ExprKind.Var => locals.TryGetValue(expr.Var.Name, out var t) ? t : throw new Exception($"Variable no declarada: {expr.Var.Name}"),
            ExprKind.Binary => InferBinary(expr.Binary, locals, functions),
            ExprKind.Call => InferCall(expr.Call, locals, functions),
            _ => throw new Exception("Expresión no soportada")
        };
    }

    private static TypeRef InferBinary(
        BinaryExpr b,
        Dictionary<string, TypeRef> locals,
        Dictionary<string, (List<TypeRef> Params, TypeRef Ret)> functions)
    {
        var lt = InferExpr(b.Left, locals, functions);
        var rt = InferExpr(b.Right, locals, functions);
        return lt == TypeRef.Int && rt == TypeRef.Int
            ? TypeRef.Int
            : throw new Exception("Operación aritmética solo con int");
    }

    private static TypeRef InferCall(
        CallExpr c,
        Dictionary<string, TypeRef> locals,
        Dictionary<string, (List<TypeRef> Params, TypeRef Ret)> functions)
    {
        if (!functions.TryGetValue(c.Name, out var sig)) throw new Exception($"Función no declarada: {c.Name}");
        if (sig.Params.Count != c.Args.Count) throw new Exception($"Aridad inválida en {c.Name}");

        for (var i = 0; i < c.Args.Count; i++)
            if (InferExpr(c.Args[i], locals, functions) != sig.Params[i])
                throw new Exception($"Tipo de argumento inválido en {c.Name}");

        return sig.Ret;
    }
}
