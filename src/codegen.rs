use std::collections::HashMap;
use std::fs;
use std::path::Path;
use std::process::Command;

use crate::ast::*;

// Resultado del backend.
pub struct BuildOutput {
    pub ir_path: String,
    pub obj_path: String,
    pub bin_path: String,
}

pub fn emit_and_build(
    program: &Program,
    out_name: &str,
    out_dir: &Path,
) -> Result<BuildOutput, String> {
    let ir = emit_ir(program)?;
    fs::create_dir_all(out_dir).map_err(|e| e.to_string())?;

    let ir_path = out_dir.join(format!("{out_name}.ll"));
    let obj_path = out_dir.join(format!("{out_name}.o"));
    let bin_path = out_dir.join(out_name);

    fs::write(&ir_path, ir).map_err(|e| e.to_string())?;

    let status = Command::new("clang")
        .arg("-c")
        .arg(&ir_path)
        .arg("-o")
        .arg(&obj_path)
        .status()
        .map_err(|e| format!("No se pudo ejecutar clang para objeto: {e}"))?;
    if !status.success() {
        return Err("clang falló generando el objeto".to_string());
    }

    let status = Command::new("clang")
        .arg(&obj_path)
        .arg("-o")
        .arg(&bin_path)
        .status()
        .map_err(|e| format!("No se pudo ejecutar clang para binario: {e}"))?;
    if !status.success() {
        return Err("clang falló enlazando el binario".to_string());
    }

    Ok(BuildOutput {
        ir_path: ir_path.display().to_string(),
        obj_path: obj_path.display().to_string(),
        bin_path: bin_path.display().to_string(),
    })
}

fn emit_ir(program: &Program) -> Result<String, String> {
    let mut out = String::new();
    out.push_str("; IR generado por NeoC\n");

    // Headers informativos para import/alias.
    for i in &program.imports {
        out.push_str(&format!("; import \"{}\"\n", i.path));
    }
    for a in &program.aliases {
        out.push_str(&format!("; alias {} = \"{}\"\n", a.name, a.path));
    }

    let mut fn_sigs = HashMap::<String, (Vec<TypeName>, TypeName)>::new();
    for item in &program.items {
        if let Item::Function(f) = item {
            fn_sigs.insert(
                f.name.clone(),
                (
                    f.params.iter().map(|p| p.ty.clone()).collect(),
                    f.ret_ty.clone(),
                ),
            );
        }
    }

    for item in &program.items {
        if let Item::Function(f) = item {
            if f.body.is_none() {
                out.push_str(&format!(
                    "declare {} @{}({})\n",
                    ir_ty(&f.ret_ty)?,
                    f.name,
                    f.params
                        .iter()
                        .map(|p| ir_ty(&p.ty))
                        .collect::<Result<Vec<_>, _>>()?
                        .join(", ")
                ));
            }
        }
    }

    for item in &program.items {
        if let Item::Function(f) = item {
            if let Some(body) = &f.body {
                let linkage = if f.is_extern || f.name == "main" {
                    ""
                } else {
                    "internal "
                };
                out.push_str(&format!(
                    "define {linkage}{} @{}({}) {{\nentry:\n",
                    ir_ty(&f.ret_ty)?,
                    f.name,
                    f.params
                        .iter()
                        .map(|p| ir_ty(&p.ty).map(|t| format!("{t} %{}", p.name)))
                        .collect::<Result<Vec<_>, _>>()?
                        .join(", ")
                ));

                let mut ctx = FnCtx::new();
                for p in &f.params {
                    let ptr = ctx.fresh_reg("p.addr");
                    out.push_str(&format!("  {ptr} = alloca {}\n", ir_ty(&p.ty)?));
                    out.push_str(&format!(
                        "  store {} %{}, {}* {ptr}\n",
                        ir_ty(&p.ty)?,
                        p.name,
                        ir_ty(&p.ty)?
                    ));
                    ctx.locals.insert(p.name.clone(), (p.ty.clone(), ptr));
                }

                for stmt in body {
                    emit_stmt(stmt, &mut ctx, &fn_sigs, &mut out)?;
                }

                if f.ret_ty == TypeName::Void {
                    out.push_str("  ret void\n");
                }
                out.push_str("}\n\n");
            }
        }
    }

    Ok(out)
}

struct FnCtx {
    idx: usize,
    locals: HashMap<String, (TypeName, String)>,
}

impl FnCtx {
    fn new() -> Self {
        Self {
            idx: 0,
            locals: HashMap::new(),
        }
    }

    fn fresh_reg(&mut self, prefix: &str) -> String {
        self.idx += 1;
        format!("%{prefix}.{}", self.idx)
    }
}

fn emit_stmt(
    stmt: &Stmt,
    ctx: &mut FnCtx,
    sigs: &HashMap<String, (Vec<TypeName>, TypeName)>,
    out: &mut String,
) -> Result<(), String> {
    match stmt {
        Stmt::Let { ty, name, value } => {
            let (expr_ty, reg) = emit_expr(value, ctx, sigs, out)?;
            if &expr_ty != ty {
                return Err(format!("Tipo incompatible en let {}", name));
            }
            let ptr = ctx.fresh_reg("v.addr");
            out.push_str(&format!("  {ptr} = alloca {}\n", ir_ty(ty)?));
            out.push_str(&format!(
                "  store {} {reg}, {}* {ptr}\n",
                ir_ty(ty)?,
                ir_ty(ty)?
            ));
            ctx.locals.insert(name.clone(), (ty.clone(), ptr));
        }
        Stmt::Return(expr) => {
            let (ty, reg) = emit_expr(expr, ctx, sigs, out)?;
            out.push_str(&format!("  ret {} {reg}\n", ir_ty(&ty)?));
        }
        Stmt::Expr(expr) => {
            let _ = emit_expr(expr, ctx, sigs, out)?;
        }
    }
    Ok(())
}

fn emit_expr(
    expr: &Expr,
    ctx: &mut FnCtx,
    sigs: &HashMap<String, (Vec<TypeName>, TypeName)>,
    out: &mut String,
) -> Result<(TypeName, String), String> {
    match expr {
        Expr::Int(n) => Ok((TypeName::Int, n.to_string())),
        Expr::Var(name) => {
            let (ty, ptr) = ctx
                .locals
                .get(name)
                .cloned()
                .ok_or_else(|| format!("Variable no declarada: {}", name))?;
            let dst = ctx.fresh_reg("load");
            out.push_str(&format!(
                "  {dst} = load {}, {}* {ptr}\n",
                ir_ty(&ty)?,
                ir_ty(&ty)?
            ));
            Ok((ty, dst))
        }
        Expr::Binary { op, lhs, rhs } => {
            let (lt, lreg) = emit_expr(lhs, ctx, sigs, out)?;
            let (rt, rreg) = emit_expr(rhs, ctx, sigs, out)?;
            if lt != TypeName::Int || rt != TypeName::Int {
                return Err("Operaciones binarias solo con int".to_string());
            }
            let dst = ctx.fresh_reg("bin");
            let ir_op = match op {
                BinOp::Add => "add",
                BinOp::Sub => "sub",
                BinOp::Mul => "mul",
                BinOp::Div => "sdiv",
            };
            out.push_str(&format!("  {dst} = {ir_op} i64 {lreg}, {rreg}\n"));
            Ok((TypeName::Int, dst))
        }
        Expr::Call { name, args } => {
            let (params, ret_ty) = sigs
                .get(name)
                .ok_or_else(|| format!("Función no declarada: {}", name))?
                .clone();
            if params.len() != args.len() {
                return Err(format!("Aridad inválida en llamada a {}", name));
            }
            let mut arg_strs = Vec::new();
            for (arg, pty) in args.iter().zip(params.iter()) {
                let (aty, areg) = emit_expr(arg, ctx, sigs, out)?;
                if &aty != pty {
                    return Err(format!("Tipo de argumento inválido en llamada a {}", name));
                }
                arg_strs.push(format!("{} {}", ir_ty(&aty)?, areg));
            }

            if ret_ty == TypeName::Void {
                out.push_str(&format!(
                    "  call {} @{}({})\n",
                    ir_ty(&ret_ty)?,
                    name,
                    arg_strs.join(", ")
                ));
                Ok((TypeName::Void, "0".to_string()))
            } else {
                let dst = ctx.fresh_reg("call");
                out.push_str(&format!(
                    "  {dst} = call {} @{}({})\n",
                    ir_ty(&ret_ty)?,
                    name,
                    arg_strs.join(", ")
                ));
                Ok((ret_ty, dst))
            }
        }
    }
}

fn ir_ty(ty: &TypeName) -> Result<String, String> {
    match ty {
        TypeName::Int => Ok("i64".to_string()),
        TypeName::Void => Ok("void".to_string()),
        TypeName::Named(n) => Err(format!(
            "Tipo de usuario aún no soportado en codegen: {}",
            n
        )),
    }
}
