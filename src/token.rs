// Definiciones de tokens para el lenguaje NeoC.
#[derive(Debug, Clone, PartialEq)]
pub enum TokenKind {
    // Símbolos
    LParen,
    RParen,
    LBrace,
    RBrace,
    Comma,
    Semicolon,
    Colon,
    Arrow,
    Plus,
    Minus,
    Star,
    Slash,
    Equal,

    // Literales e identificadores
    Ident(String),
    IntLit(i64),
    StringLit(String),

    // Palabras clave
    Fn,
    Struct,
    Extern,
    Import,
    Alias,
    Return,
    Let,

    Eof,
}

#[derive(Debug, Clone)]
pub struct Token {
    pub kind: TokenKind,
    pub line: usize,
    pub col: usize,
}

impl Token {
    pub fn new(kind: TokenKind, line: usize, col: usize) -> Self {
        Self { kind, line, col }
    }
}
