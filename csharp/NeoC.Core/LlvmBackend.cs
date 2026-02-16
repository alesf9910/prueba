using System.Diagnostics;
using System.Text;

namespace NeoC.Core;

public readonly record struct BuildOutput(string IrPath, string ObjPath, string BinPath);

// Backend nativo: LLVM IR textual + clang (objeto/binario).
public static partial struct LlvmBackend
{
    public static BuildOutput Build(ProgramNode program, string outDir, string outName)
    {
        Directory.CreateDirectory(outDir);
        var irPath = Path.Combine(outDir, $"{outName}.ll");
        var objPath = Path.Combine(outDir, $"{outName}.o");
        var binPath = Path.Combine(outDir, outName);

        File.WriteAllText(irPath, EmitIr(program));
        Run("clang", $"-c \"{irPath}\" -o \"{objPath}\"");
        Run("clang", $"\"{objPath}\" -o \"{binPath}\"");

        return new(irPath, objPath, binPath);
    }

    private static string EmitIr(ProgramNode program)
    {
        var sb = new StringBuilder("; NeoC LLVM IR\n");

        foreach (var importNode in program.Imports)
            sb.AppendLine($"; import \"{importNode.Path}\"");
        foreach (var alias in program.Aliases)
            sb.AppendLine($"; alias {alias.Name} = \"{alias.Path}\"");

        var signatures = new Dictionary<string, (List<TypeRef> Params, TypeRef Ret)>();
        foreach (var item in program.Items)
        {
            if (item.Kind != ItemKind.Function) continue;
            var fn = item.Function;
            signatures[fn.Name] = (fn.Parameters.Select(p => p.Type).ToList(), fn.ReturnType);
        }

        foreach (var item in program.Items)
        {
            if (item.Kind != ItemKind.Function) continue;
            var fn = item.Function;
            if (fn.Body is null)
                sb.AppendLine($"declare {Ty(fn.ReturnType)} @{fn.Name}({string.Join(", ", fn.Parameters.Select(p => Ty(p.Type)))})");
        }

        foreach (var item in program.Items)
        {
            if (item.Kind != ItemKind.Function) continue;
            var fn = item.Function;
            if (fn.Body is null) continue;

            var linkage = (fn.IsExtern || fn.Name == "main") ? string.Empty : "internal ";
            sb.AppendLine($"define {linkage}{Ty(fn.ReturnType)} @{fn.Name}({string.Join(", ", fn.Parameters.Select(p => $"{Ty(p.Type)} %{p.Name}"))}) {{");
            sb.AppendLine("entry:");

            var cg = new CgContext(0, new Dictionary<string, (TypeRef Type, string Addr)>());
            foreach (var p in fn.Parameters)
            {
                var addr = Fresh(ref cg, "p.addr");
                sb.AppendLine($"  {addr} = alloca {Ty(p.Type)}");
                sb.AppendLine($"  store {Ty(p.Type)} %{p.Name}, {Ty(p.Type)}* {addr}");
                cg.Locals[p.Name] = (p.Type, addr);
            }

            foreach (var stmt in fn.Body)
                EmitStmt(stmt, sb, ref cg, signatures);

            if (fn.ReturnType == TypeRef.Void)
                sb.AppendLine("  ret void");

            sb.AppendLine("}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void EmitStmt(
        StmtNode stmt,
        StringBuilder sb,
        ref CgContext cg,
        Dictionary<string, (List<TypeRef> Params, TypeRef Ret)> signatures)
    {
        switch (stmt.Kind)
        {
            case StmtKind.Let:
            {
                var s = stmt.Let;
                var (type, reg) = EmitExpr(s.Value, sb, ref cg, signatures);
                if (type != s.Type) throw new Exception($"Tipo incompatible en let {s.Name}");
                var addr = Fresh(ref cg, "v.addr");
                sb.AppendLine($"  {addr} = alloca {Ty(s.Type)}");
                sb.AppendLine($"  store {Ty(s.Type)} {reg}, {Ty(s.Type)}* {addr}");
                cg.Locals[s.Name] = (s.Type, addr);
                break;
            }
            case StmtKind.Return:
            {
                var s = stmt.Return;
                var (type, reg) = EmitExpr(s.Value, sb, ref cg, signatures);
                sb.AppendLine($"  ret {Ty(type)} {reg}");
                break;
            }
            case StmtKind.Expr:
                _ = EmitExpr(stmt.Expr.Value, sb, ref cg, signatures);
                break;
        }
    }

    private static (TypeRef Type, string Reg) EmitExpr(
        ExprNode expr,
        StringBuilder sb,
        ref CgContext cg,
        Dictionary<string, (List<TypeRef> Params, TypeRef Ret)> signatures)
    {
        return expr.Kind switch
        {
            ExprKind.Int => (TypeRef.Int, expr.Int.Value.ToString()),
            ExprKind.Var => EmitVar(expr.Var, sb, ref cg),
            ExprKind.Binary => EmitBinary(expr.Binary, sb, ref cg, signatures),
            ExprKind.Call => EmitCall(expr.Call, sb, ref cg, signatures),
            _ => throw new Exception("Expresión no soportada")
        };
    }

    private static (TypeRef, string) EmitVar(VarExpr v, StringBuilder sb, ref CgContext cg)
    {
        if (!cg.Locals.TryGetValue(v.Name, out var data))
            throw new Exception($"Variable no declarada: {v.Name}");

        var reg = Fresh(ref cg, "load");
        sb.AppendLine($"  {reg} = load {Ty(data.Type)}, {Ty(data.Type)}* {data.Addr}");
        return (data.Type, reg);
    }

    private static (TypeRef, string) EmitBinary(
        BinaryExpr b,
        StringBuilder sb,
        ref CgContext cg,
        Dictionary<string, (List<TypeRef> Params, TypeRef Ret)> signatures)
    {
        var (lt, lreg) = EmitExpr(b.Left, sb, ref cg, signatures);
        var (rt, rreg) = EmitExpr(b.Right, sb, ref cg, signatures);

        if (lt != TypeRef.Int || rt != TypeRef.Int)
            throw new Exception("Operación binaria solo int");

        var reg = Fresh(ref cg, "bin");
        var op = b.Op switch
        {
            BinaryOp.Add => "add",
            BinaryOp.Sub => "sub",
            BinaryOp.Mul => "mul",
            _ => "sdiv"
        };

        sb.AppendLine($"  {reg} = {op} i64 {lreg}, {rreg}");
        return (TypeRef.Int, reg);
    }

    private static (TypeRef, string) EmitCall(
        CallExpr c,
        StringBuilder sb,
        ref CgContext cg,
        Dictionary<string, (List<TypeRef> Params, TypeRef Ret)> signatures)
    {
        if (!signatures.TryGetValue(c.Name, out var sig))
            throw new Exception($"Función no declarada: {c.Name}");

        var args = new List<string>();
        for (var i = 0; i < c.Args.Count; i++)
        {
            var (argType, argReg) = EmitExpr(c.Args[i], sb, ref cg, signatures);
            if (argType != sig.Params[i])
                throw new Exception($"Tipo de argumento inválido en {c.Name}");
            args.Add($"{Ty(argType)} {argReg}");
        }

        if (sig.Ret == TypeRef.Void)
        {
            sb.AppendLine($"  call void @{c.Name}({string.Join(", ", args)})");
            return (TypeRef.Void, "0");
        }

        var reg = Fresh(ref cg, "call");
        sb.AppendLine($"  {reg} = call {Ty(sig.Ret)} @{c.Name}({string.Join(", ", args)})");
        return (sig.Ret, reg);
    }

    private static string Fresh(ref CgContext cg, string prefix)
    {
        cg.Index += 1;
        return $"%{prefix}.{cg.Index}";
    }

    private static string Ty(TypeRef type)
    {
        return type.Kind switch
        {
            TypeKind.Int => "i64",
            TypeKind.Void => "void",
            _ => throw new Exception($"Tipo no soportado en backend LLVM actual: {type.Name}")
        };
    }

    private static void Run(string file, string args)
    {
        var proc = Process.Start(new ProcessStartInfo(file, args)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        }) ?? throw new Exception($"No se pudo iniciar {file}");

        proc.WaitForExit();
        if (proc.ExitCode != 0)
            throw new Exception($"Falló {file}: {proc.StandardError.ReadToEnd()}");
    }

    private struct CgContext(int index, Dictionary<string, (TypeRef Type, string Addr)> locals)
    {
        public int Index = index;
        public Dictionary<string, (TypeRef Type, string Addr)> Locals = locals;
    }
}
