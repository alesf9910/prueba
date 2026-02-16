use std::collections::{HashMap, HashSet};

use crate::ast::*;

// Análisis semántico básico: símbolos globales y tipos.
pub fn check(program: &Program) -> Result<(), String> {
    let mut structs = HashSet::new();
    let mut funcs: HashMap<String, (Vec<TypeName>, TypeName)> = HashMap::new();

    for item in &program.items {
        match item {
            Item::Struct(s) => {
                if !structs.insert(s.name.clone()) {
                    return Err(format!("Struct duplicada: {}", s.name));
                }
            }
            Item::Function(f) => {
                if funcs.contains_key(&f.name) {
                    return Err(format!("Función duplicada: {}", f.name));
                }
                let params = f.params.iter().map(|p| p.ty.clone()).collect::<Vec<_>>();
                funcs.insert(f.name.clone(), (params, f.ret_ty.clone()));
            }
        }
    }

    for item in &program.items {
        if let Item::Function(f) = item {
            if let Some(body) = &f.body {
                let mut locals: HashMap<String, TypeName> = HashMap::new();
                for p in &f.params {
                    locals.insert(p.name.clone(), p.ty.clone());
                }

                for stmt in body {
                    check_stmt(stmt, &f.ret_ty, &mut locals, &funcs)?;
                }
            }
        }
    }

    Ok(())
}

fn check_stmt(
    stmt: &Stmt,
    fn_ret: &TypeName,
    locals: &mut HashMap<String, TypeName>,
    funcs: &HashMap<String, (Vec<TypeName>, TypeName)>,
) -> Result<(), String> {
    match stmt {
        Stmt::Let { ty, name, value } => {
            let expr_ty = infer_expr(value, locals, funcs)?;
            if &expr_ty != ty {
                return Err(format!("Tipo incompatible en let {}", name));
            }
            locals.insert(name.clone(), ty.clone());
        }
        Stmt::Return(expr) => {
            let ret = infer_expr(expr, locals, funcs)?;
            if &ret != fn_ret {
                return Err("Tipo de return incorrecto".to_string());
            }
        }
        Stmt::Expr(expr) => {
            let _ = infer_expr(expr, locals, funcs)?;
        }
    }
    Ok(())
}

fn infer_expr(
    expr: &Expr,
    locals: &HashMap<String, TypeName>,
    funcs: &HashMap<String, (Vec<TypeName>, TypeName)>,
) -> Result<TypeName, String> {
    match expr {
        Expr::Int(_) => Ok(TypeName::Int),
        Expr::Var(name) => locals
            .get(name)
            .cloned()
            .ok_or_else(|| format!("Variable no declarada: {}", name)),
        Expr::Binary { lhs, rhs, .. } => {
            let lt = infer_expr(lhs, locals, funcs)?;
            let rt = infer_expr(rhs, locals, funcs)?;
            if lt == TypeName::Int && rt == TypeName::Int {
                Ok(TypeName::Int)
            } else {
                Err("Las operaciones aritméticas solo soportan int".to_string())
            }
        }
        Expr::Call { name, args } => {
            let (params, ret) = funcs
                .get(name)
                .ok_or_else(|| format!("Función no declarada: {}", name))?;
            if params.len() != args.len() {
                return Err(format!("Aridad inválida en llamada a {}", name));
            }
            for (arg, pty) in args.iter().zip(params) {
                let aty = infer_expr(arg, locals, funcs)?;
                if &aty != pty {
                    return Err(format!("Tipo de argumento inválido en {}", name));
                }
            }
            Ok(ret.clone())
        }
    }
}
