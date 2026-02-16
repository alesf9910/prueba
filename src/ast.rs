#[derive(Debug, Clone)]
pub struct Program {
    pub imports: Vec<Import>,
    pub aliases: Vec<Alias>,
    pub items: Vec<Item>,
}

#[derive(Debug, Clone)]
pub struct Import {
    pub path: String,
}

#[derive(Debug, Clone)]
pub struct Alias {
    pub name: String,
    pub path: String,
}

#[derive(Debug, Clone)]
pub enum Item {
    Struct(StructDecl),
    Function(FunctionDecl),
}

#[derive(Debug, Clone)]
pub struct StructDecl {
    pub name: String,
    pub fields: Vec<FieldDecl>,
    pub is_extern: bool,
}

#[derive(Debug, Clone)]
pub struct FieldDecl {
    pub ty: TypeName,
    pub name: String,
}

#[derive(Debug, Clone)]
pub struct FunctionDecl {
    pub name: String,
    pub params: Vec<ParamDecl>,
    pub ret_ty: TypeName,
    pub body: Option<Vec<Stmt>>, // None = declaración externa
    pub is_extern: bool,
}

#[derive(Debug, Clone)]
pub struct ParamDecl {
    pub ty: TypeName,
    pub name: String,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum TypeName {
    Int,
    Void,
    Named(String),
}

#[derive(Debug, Clone)]
pub enum Stmt {
    Let {
        ty: TypeName,
        name: String,
        value: Expr,
    },
    Return(Expr),
    Expr(Expr),
}

#[derive(Debug, Clone)]
pub enum Expr {
    Int(i64),
    Var(String),
    Binary {
        op: BinOp,
        lhs: Box<Expr>,
        rhs: Box<Expr>,
    },
    Call {
        name: String,
        args: Vec<Expr>,
    },
}

#[derive(Debug, Clone)]
pub enum BinOp {
    Add,
    Sub,
    Mul,
    Div,
}
