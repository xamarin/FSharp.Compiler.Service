(*** hide ***)
#I "../../bin/v4.5/"
(**
Compiler Services: Processing typed expression tree
=================================================

This tutorial demonstrates how to get the checked, typed expressions tree (TAST)
for F# code and how to walk over the tree. 

This can be used for creating tools such as source code analyzers and refactoring tools.
You can also combine the information with the API available
from [symbols](symbols.html). 

> **NOTE:** The FSharp.Compiler.Service API is subject to change when later versions of the nuget package are published


Getting checked expressions
-----------------------

To access the type-checked, resolved expressions, you need to create an instance of `InteractiveChecker`.

To use the interactive checker, reference `FSharp.Compiler.Service.dll` and open the
`SourceCodeServices` namespace:
*)
#r "FSharp.Compiler.Service.dll"
open System
open System.IO
open Microsoft.FSharp.Compiler.SourceCodeServices
(**

### Checking code

We first parse and check some code as in the [symbols](symbols.html) tutorial.
One difference is that we set keepAssemblyContents to true.

*)
// Create an interactive checker instance 
let checker = FSharpChecker.Create(keepAssemblyContents=true)

let parseAndCheckSingleFile (input) = 
    let file = Path.ChangeExtension(System.IO.Path.GetTempFileName(),"fsx")  
    File.WriteAllText(file, input)
    // Get context representing a stand-alone (script) file
    let projOptions = 
        checker.GetProjectOptionsFromScript(file, input)
        |> Async.RunSynchronously

    checker.ParseAndCheckProject(projOptions) 
    |> Async.RunSynchronously

(**
## Getting the expressions

After type checking a file, you can access the declarations and contents of the assembly, including expressions:

*)

let input2 = 
      """
module MyLibrary 

open System

let foo(x, y) = 
    let msg = String.Concat("Hello"," ","world")
    if msg.Length > 10 then 
        10 
    else 
        20

type MyClass() = 
    member x.MyMethod() = 1
      """
let checkProjectResults = 
    parseAndCheckSingleFile(input2)

checkProjectResults.Errors // should be empty


(**

Checked assemblies are made up of a series of checked implementation files.  The "file" granularity
matters in F# because initialization actions are triggered at the granularity of files.
In this case there is only one implementation file in the project:

*)

let checkedFile = checkProjectResults.AssemblyContents.ImplementationFiles.[0]

(**

Checked assemblies are made up of a series of checked implementation files.  The "file" granularity
matters in F# because initialization actions are triggered at the granularity of files.
In this case there is only one implementation file in the project:

*)

let rec printDecl prefix d = 
    match d with 
    | FSharpImplementationFileDeclaration.Entity (e,subDecls) -> 
        printfn "%sEntity %s was declared and contains %d sub-declarations" prefix e.CompiledName subDecls.Length
        for subDecl in subDecls do 
            printDecl (prefix+"    ") subDecl
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v,vs,e) -> 
        printfn "%sMember or value %s was declared" prefix  v.CompiledName
    | FSharpImplementationFileDeclaration.InitAction(e) -> 
        printfn "%sA top-level expression was declared" prefix 


for d in checkedFile.Declarations do 
   printDecl "" d

// Entity MyLibrary was declared and contains 4 sub-declarations
//     Member or value foo was declared
//     Entity MyClass was declared and contains 0 sub-declarations
//     Member or value .ctor was declared
//     Member or value MyMethod was declared

(**

As can be seen, the only declaration in the implementation file is that of the module MyLibrary, which 
contains fours sub-declarations.  

> As an aside, one peculiarity here is that the member declarations (e.g. the "MyMethod" member) are returned as part of the containing module entity, not as part of their class.

> Note that the class constructor is returned as a separate declaration. The class type definition has been "split" into a constructor and the other declarations.

*)

let myLibraryEntity, myLibraryDecls =    
   match checkedFile.Declarations.[0] with 
   | FSharpImplementationFileDeclaration.Entity (e,subDecls) -> (e,subDecls)
   | _ -> failwith "unexpected"


(**

What about the expressions, for example the body of function "foo"? Let's find it:
*)

let (fooSymbol, fooArgs, fooExpression) = 
    match myLibraryDecls.[0] with 
    | FSharpImplementationFileDeclaration.MemberOrFunctionOrValue(v,vs,e) -> (v,vs,e)
    | _ -> failwith "unexpected"


(** Here 'fooSymbol' is a symbold associated with the declaration of 'foo', 
'fooArgs' represents the formal arguments to the 'foo' function, and 'fooExpression' 
is an expression for the implementation of the 'foo' function.

Once you have an expression, you can work with it much like an F# quotation.  For example,
you can find its declaration range and its type:

*)

fooExpression.Type  // shows that the return type of the body expression is 'int'
fooExpression.Range  // shows the declaration range of the expression implementing 'foo'

(**

### Walking over expressions


Expressions are analyzed using active patterns, much like F# quotations.
Here is a generic expression visitor:

*)

let rec visitExpr f (e:FSharpExpr) = 
    f e
    match e with 
    | BasicPatterns.AddressOf(e1) -> visitExpr f e1
    | BasicPatterns.AddressSet(e1,e2) -> visitExpr f e1; visitExpr f e2
    | BasicPatterns.Application(ef,tyargs,args) -> visitExpr f ef; visitExprs f args
    | BasicPatterns.Call(obj,v,tyargs1,tyargs2,args) -> visitObjArg f obj; visitExprs f args
    | BasicPatterns.Coerce(ty1,e1) -> visitExpr f e1
    | BasicPatterns.FastIntegerForLoop(e1,e2,e3,isUp) -> visitExpr f e1; visitExpr f e2; visitExpr f e3
    | BasicPatterns.ILAsm(s,tyargs,args) -> visitExprs f args
    | BasicPatterns.ILFieldGet (objOpt, fieldType, fieldName) -> visitObjArg f objOpt
    | BasicPatterns.ILFieldSet (objOpt, fieldType, fieldName, ve) -> visitObjArg f objOpt
    | BasicPatterns.IfThenElse (ge,te,ee) -> visitExpr f ge; visitExpr f te; visitExpr f ee
    | BasicPatterns.Lambda(v,body) -> visitExpr f body
    | BasicPatterns.Let((v,ve),body) -> visitExpr f ve; visitExpr f body
    | BasicPatterns.LetRec(vse,body) -> List.iter (snd >> visitExpr f) vse; visitExpr f body
    | BasicPatterns.NewArray(ty,args) -> visitExprs f args
    | BasicPatterns.NewDelegate(ty,arg) -> visitExpr f arg
    | BasicPatterns.NewObject(v,tys,args) -> visitExprs f args
    | BasicPatterns.NewRecord(v,args) ->  visitExprs f args
    | BasicPatterns.NewTuple(v,args) -> visitExprs f args
    | BasicPatterns.NewUnionCase(ty,uc,args) -> visitExprs f args
    | BasicPatterns.Quote(e1) -> visitExpr f e1
    | BasicPatterns.FSharpFieldGet(objOpt, ty,fieldInfo) -> visitObjArg f objOpt
    | BasicPatterns.FSharpFieldSet(objOpt, ty,fieldInfo,arg) -> visitObjArg f objOpt; visitExpr f arg
    | BasicPatterns.Sequential(e1,e2) -> visitExpr f e1; visitExpr f e2
    | BasicPatterns.TryFinally(e1,e2) -> visitExpr f e1; visitExpr f e2
    | BasicPatterns.TryWith(body,_,_,vCatch,eCatch) -> visitExpr f body; visitExpr f eCatch
    | BasicPatterns.TupleGet(ty,n,e1) -> visitExpr f e1
    | BasicPatterns.DecisionTree(dtree,targets) -> visitExpr f dtree; List.iter (snd >> visitExpr f) targets
    | BasicPatterns.DecisionTreeSuccess (tg,es) -> visitExprs f es
    | BasicPatterns.TypeLambda(gp1,body) -> visitExpr f body
    | BasicPatterns.TypeTest(ty,e1) -> visitExpr f e1
    | BasicPatterns.UnionCaseSet(obj,ty,uc,f1,e1) -> visitExpr f obj; visitExpr f e1
    | BasicPatterns.UnionCaseGet(obj,ty,uc,f1) -> visitExpr f obj
    | BasicPatterns.UnionCaseTest(obj,ty,f1) -> visitExpr f obj
    | BasicPatterns.UnionCaseTag(obj,ty) -> visitExpr f obj
    | BasicPatterns.ObjectExpr(ty,basecall,overrides,iimpls) -> 
        visitExpr f basecall
        List.iter (visitObjMember f) overrides
        List.iter (snd >> List.iter (visitObjMember f)) iimpls
    | BasicPatterns.TraitCall(tys,nm,argtys,tinst,args) -> visitExprs f args
    | BasicPatterns.ValueSet(v,e1) -> visitExpr f e1
    | BasicPatterns.WhileLoop(e1,e2) -> visitExpr f e1; visitExpr f e2
    | BasicPatterns.BaseValue _ -> ()
    | BasicPatterns.DefaultValue _ -> ()
    | BasicPatterns.ThisValue _ -> ()
    | BasicPatterns.Const(obj,ty) -> ()
    | BasicPatterns.Value(v) -> ()
    | _ -> failwith (sprintf "unrecognized %+A" e)

and visitExprs f exprs = 
    List.iter (visitExpr f) exprs

and visitObjArg f objOpt = 
    Option.iter (visitExpr f) objOpt

and visitObjMember f memb = 
    visitExpr f memb.Body

(**
Let's use this expresssion walker:

*)
fooExpression |> visitExpr (fun e -> printfn "Visiting %A" e)

// Prints:
//
// Visiting Let...
// Visiting Call...
// Visiting Const ("Hello",...)
// Visiting Const (" ",...)
// Visiting Const ("world",...)
// Visiting IfThenElse...
// Visiting Call...
// Visiting Call...
// Visiting Value ...
// Visiting Const ...
// Visiting Const ...
// Visiting Const ...

(**
Note that 

* The visitExpr function is recursive (for nested expressions).

* Pattern matching is removed from the tree, into a form called 'decision trees'. 

Summary
-------
In this tutorial, we looked at basic of working with checked declarations and expressions. 

In practice, it is also useful to combine the information here
with some information you can obtain from the [symbols](symbols.html) 
tutorial.
*)
