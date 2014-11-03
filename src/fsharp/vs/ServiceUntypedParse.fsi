// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

//----------------------------------------------------------------------------
// API to the compiler as an incremental service for parsing,
// type checking and intellisense-like environment-reporting.
//----------------------------------------------------------------------------

namespace Microsoft.FSharp.Compiler.SourceCodeServices

open Microsoft.FSharp.Compiler 
open Microsoft.FSharp.Compiler.Range
open Microsoft.FSharp.Compiler.ErrorLogger
open System.Collections.Generic

[<Sealed>]
/// Represents the results of parsing an F# file
type FSharpParseFileResults = 
    member ParseTree : Ast.ParsedInput option
    /// Notable parse info for ParameterInfo at a given location
    member FindNoteworthyParamInfoLocations : pos:pos -> FSharpNoteworthyParamInfoLocations option
    /// Name of the file for which this information were created
    member FileName                       : string
    /// Get declared items and the selected item at the specified location
    member GetNavigationItems             : unit -> FSharpNavigationItems
    /// Return the inner-most range associated with a possible breakpoint location
    member ValidateBreakpointLocation : pos:pos -> range option
    /// When these files change then the build is invalid
    member DependencyFiles : string list

    /// Get the errors and warnings for the parse
    member Errors : FSharpErrorInfo[]

    /// Indicates if any errors occured during the parse
    member ParseHadErrors : bool

    internal new : errors : FSharpErrorInfo[] * input : Ast.ParsedInput option * parseHadErrors : bool * dependencyFiles : string list -> FSharpParseFileResults

/// Information about F# source file names
module internal SourceFile =
   /// Whether or not this file is compilable
   val IsCompilable : string -> bool
   /// Whether or not this file should be a single-file project
   val MustBeSingleFileProject : string -> bool

type internal CompletionPath = string list * string option // plid * residue

type internal InheritanceContext = 
    | Class
    | Interface
    | Unknown

type internal RecordContext =
    | CopyOnUpdate of range * CompletionPath // range
    | Constructor of string // typename
    | New of CompletionPath

type internal CompletionContext = 
    // completion context cannot be determined due to errors
    | Invalid
    // completing something after the inherit keyword
    | Inherit of InheritanceContext * CompletionPath
    // completing records field
    | RecordField of RecordContext
    | RangeOperator


// implementation details used by other code in the compiler    
module (*internal*) UntypedParseImpl =
    open Microsoft.FSharp.Compiler.Ast
    val TryFindExpressionASTLeftOfDotLeftOfCursor : pos * ParsedInput option -> (pos * bool) option
    val GetRangeOfExprLeftOfDot : pos  * ParsedInput option -> range option
    val TryFindExpressionIslandInPosition : pos * ParsedInput option -> string option
    val TryGetCompletionContext : pos * FSharpParseFileResults option -> CompletionContext option

// implementation details used by other code in the compiler    
module internal SourceFileImpl =
    val IsInterfaceFile : string -> bool 
    val AdditionalDefinesForUseInEditor : string -> string list

[<System.Obsolete("This type has been renamed to FSharpParseFileResults")>]
/// Renamed to FSharpParseFileResults
type ParseFileResults = FSharpParseFileResults
