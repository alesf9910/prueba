namespace NeoC.Core;

// Parser recursivo descendente con estado en ref struct.
public ref struct Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _i;

    public Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
        _i = 0;
    }

    private Token Cur => _tokens[_i];

    public ProgramNode ParseProgram()
    {
        var imports = new List<ImportNode>();
        var aliases = new List<AliasNode>();
        var items = new List<ItemNode>();

        while (Cur.Kind != TokenKind.Eof)
        {
            if (Match(TokenKind.Import)) imports.Add(ParseImport());
            else if (Match(TokenKind.Alias)) aliases.Add(ParseAlias());
            else items.Add(ParseItem());
        }

        return new(imports, aliases, items);
    }

    private ImportNode ParseImport()
    {
        var path = Expect(TokenKind.StringLit, "string en import").Lexeme;
        Expect(TokenKind.Semicolon, "';' en import");
        return new(path);
    }

    private AliasNode ParseAlias()
    {
        var name = Expect(TokenKind.Ident, "identificador de alias").Lexeme;
        Expect(TokenKind.Equal, "'=' en alias");
        var path = Expect(TokenKind.StringLit, "string en alias").Lexeme;
        Expect(TokenKind.Semicolon, "';' en alias");
        return new(name, path);
    }

    private ItemNode ParseItem()
    {
        var isExtern = Match(TokenKind.Extern);
        if (Match(TokenKind.Struct)) return ItemNode.FromStruct(ParseStruct(isExtern));
        if (Match(TokenKind.Fn)) return ItemNode.FromFunction(ParseFunction(isExtern));
        throw Error("item inválido");
    }

    private StructNode ParseStruct(bool isExtern)
    {
        var name = Expect(TokenKind.Ident, "nombre de struct").Lexeme;
        Expect(TokenKind.LBrace, "'{' en struct");

        var fields = new List<FieldNode>();
        while (Cur.Kind != TokenKind.RBrace)
        {
            var ty = ParseType();
            var fieldName = Expect(TokenKind.Ident, "campo").Lexeme;
            Expect(TokenKind.Semicolon, "';' en campo");
            fields.Add(new(ty, fieldName));
        }

        Expect(TokenKind.RBrace, "'}' en struct");
        Expect(TokenKind.Semicolon, "';' tras struct");
        return new(name, fields, isExtern);
    }

    private FunctionNode ParseFunction(bool isExtern)
    {
        var name = Expect(TokenKind.Ident, "nombre de función").Lexeme;
        Expect(TokenKind.LParen, "'('");

        var parameters = new List<ParamNode>();
        if (Cur.Kind != TokenKind.RParen)
        {
            do
            {
                var ty = ParseType();
                var pName = Expect(TokenKind.Ident, "parámetro").Lexeme;
                parameters.Add(new(ty, pName));
            } while (Match(TokenKind.Comma));
        }

        Expect(TokenKind.RParen, "')'");
        Expect(TokenKind.Arrow, "'->'");
        var ret = ParseType();

        if (Match(TokenKind.Semicolon))
        {
            return new(name, parameters, ret, null, true);
        }

        Expect(TokenKind.LBrace, "'{' en cuerpo");
        var body = new List<StmtNode>();
        while (Cur.Kind != TokenKind.RBrace)
        {
            body.Add(ParseStmt());
        }
        Expect(TokenKind.RBrace, "'}' en cuerpo");

        return new(name, parameters, ret, body, isExtern);
    }

    private StmtNode ParseStmt()
    {
        if (Match(TokenKind.Let))
        {
            var ty = ParseType();
            var name = Expect(TokenKind.Ident, "variable").Lexeme;
            Expect(TokenKind.Equal, "'=' en let");
            var expr = ParseExpr();
            Expect(TokenKind.Semicolon, "';' en let");
            return StmtNode.FromLet(new(ty, name, expr));
        }

        if (Match(TokenKind.Return))
        {
            var expr = ParseExpr();
            Expect(TokenKind.Semicolon, "';' en return");
            return StmtNode.FromReturn(new(expr));
        }

        var value = ParseExpr();
        Expect(TokenKind.Semicolon, "';' en expresión");
        return StmtNode.FromExpr(new(value));
    }

    private ExprNode ParseExpr() => ParseAddSub();

    private ExprNode ParseAddSub()
    {
        var node = ParseMulDiv();
        while (Cur.Kind is TokenKind.Plus or TokenKind.Minus)
        {
            var op = Cur.Kind == TokenKind.Plus ? BinaryOp.Add : BinaryOp.Sub;
            Next();
            node = ExprNode.FromBinary(new(op, node, ParseMulDiv()));
        }

        return node;
    }

    private ExprNode ParseMulDiv()
    {
        var node = ParsePrimary();
        while (Cur.Kind is TokenKind.Star or TokenKind.Slash)
        {
            var op = Cur.Kind == TokenKind.Star ? BinaryOp.Mul : BinaryOp.Div;
            Next();
            node = ExprNode.FromBinary(new(op, node, ParsePrimary()));
        }
        return node;
    }

    private ExprNode ParsePrimary()
    {
        if (Cur.Kind == TokenKind.IntLit)
        {
            var value = long.Parse(Cur.Lexeme);
            Next();
            return ExprNode.FromInt(new(value));
        }

        if (Cur.Kind == TokenKind.Ident)
        {
            var name = Cur.Lexeme;
            Next();
            if (Match(TokenKind.LParen))
            {
                var args = new List<ExprNode>();
                if (Cur.Kind != TokenKind.RParen)
                {
                    do { args.Add(ParseExpr()); } while (Match(TokenKind.Comma));
                }
                Expect(TokenKind.RParen, "')' en llamada");
                return ExprNode.FromCall(new(name, args));
            }

            return ExprNode.FromVar(new(name));
        }

        if (Match(TokenKind.LParen))
        {
            var inner = ParseExpr();
            Expect(TokenKind.RParen, "')' de agrupación");
            return inner;
        }

        throw Error("expresión inválida");
    }

    private TypeRef ParseType()
    {
        var id = Expect(TokenKind.Ident, "tipo").Lexeme;
        return id switch
        {
            "int" => TypeRef.Int,
            "void" => TypeRef.Void,
            _ => TypeRef.Named(id)
        };
    }

    private bool Match(TokenKind kind)
    {
        if (Cur.Kind == kind)
        {
            Next();
            return true;
        }
        return false;
    }

    private Token Expect(TokenKind kind, string msg)
        => Cur.Kind == kind ? Next() : throw Error($"Se esperaba {msg}");

    private Token Next()
    {
        var t = Cur;
        if (_i < _tokens.Count - 1) _i++;
        return t;
    }

    private Exception Error(string msg) => new($"{msg} en {Cur.Line}:{Cur.Column}");
}
