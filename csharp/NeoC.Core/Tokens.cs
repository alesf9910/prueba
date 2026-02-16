namespace NeoC.Core;

public enum TokenKind
{
    LParen, RParen, LBrace, RBrace, Comma, Semicolon, Arrow, Plus, Minus, Star, Slash, Equal,
    Fn, Struct, Extern, Import, Alias, Return, Let,
    Ident, IntLit, StringLit, Eof
}

public sealed record Token(TokenKind Kind, string Lexeme, int Line, int Column);
