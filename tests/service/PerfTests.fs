﻿#if INTERACTIVE
#r "../../bin/v4.5/FSharp.Compiler.Service.dll"
#r "../../packages/NUnit.2.6.3/lib/nunit.framework.dll"
#load "FsUnit.fs"
#load "Common.fs"
#else
module FSharp.Compiler.Service.Tests.PerfTests
#endif


open NUnit.Framework
open FsUnit
open System
open System.IO
open System.Collections.Generic

open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices

open FSharp.Compiler.Service.Tests.Common

// Create an interactive checker instance 
let checker = InteractiveChecker.Create()

module Project1 = 
    open System.IO

    let fileNamesI = [ for i in 1 .. 10 -> (i, Path.ChangeExtension(Path.GetTempFileName(), ".fs")) ]
    let base2 = Path.GetTempFileName()
    let dllName = Path.ChangeExtension(base2, ".dll")
    let projFileName = Path.ChangeExtension(base2, ".fsproj")
    let fileSources = [ for (i,f) in fileNamesI -> (f, "module M" + string i) ]
    for (f,text) in fileSources do File.WriteAllText(f, text)
    let fileSources2 = [ for (i,f) in fileSources -> f ]

    let fileNames = [ for (_,f) in fileNamesI -> f ]
    let args = mkProjectCommandLineArgs (dllName, fileNames)
    let options =  checker.GetProjectOptionsFromCommandLineArgs (projFileName, args)


[<Test>]
let ``Test request for parse and check doesn't check whole project`` () = 

    let backgroundParseCount = ref 0 
    let backgroundCheckCount = ref 0 
    checker.FileChecked.Add (fun x -> incr backgroundCheckCount)
    checker.FileParsed.Add (fun x -> incr backgroundParseCount)

    let pB, tB = InteractiveChecker.GlobalForegroundParseCountStatistic, InteractiveChecker.GlobalForegroundTypeCheckCountStatistic
    let parseResults1 = checker.ParseFileInProject(Project1.fileNames.[5], Project1.fileSources2.[5], Project1.options)  |> Async.RunSynchronously
    let pC, tC = InteractiveChecker.GlobalForegroundParseCountStatistic, InteractiveChecker.GlobalForegroundTypeCheckCountStatistic
    (pC - pB) |> shouldEqual 1
    (tC - tB) |> shouldEqual 0
    backgroundParseCount.Value |> shouldEqual 0
    backgroundCheckCount.Value |> shouldEqual 0
    let checkResults1 = checker.CheckFileInProject(parseResults1, Project1.fileNames.[5], 0, Project1.fileSources2.[5], Project1.options)  |> Async.RunSynchronously
    let pD, tD = InteractiveChecker.GlobalForegroundParseCountStatistic, InteractiveChecker.GlobalForegroundTypeCheckCountStatistic
    backgroundParseCount.Value |> shouldEqual 10 // This could be reduced to 5 - the whole project gets parsed 
    backgroundCheckCount.Value |> shouldEqual 5
    (pD - pC) |> shouldEqual 0
    (tD - tC) |> shouldEqual 1

    let checkResults2 = checker.CheckFileInProject(parseResults1, Project1.fileNames.[7], 0, Project1.fileSources2.[7], Project1.options)  |> Async.RunSynchronously
    let pE, tE = InteractiveChecker.GlobalForegroundParseCountStatistic, InteractiveChecker.GlobalForegroundTypeCheckCountStatistic
    (pE - pD) |> shouldEqual 0
    (tE - tD) |> shouldEqual 1
    backgroundParseCount.Value |> shouldEqual 10 // but note, the project does not get reparsed
    backgroundCheckCount.Value |> shouldEqual 7 // only two extra typechecks of files

    // A subsequent ParseAndCheck of identical source code doesn't do any more anything
    let checkResults2 = checker.ParseAndCheckFileInProject(Project1.fileNames.[7], 0, Project1.fileSources2.[7], Project1.options)  |> Async.RunSynchronously
    let pF, tF = InteractiveChecker.GlobalForegroundParseCountStatistic, InteractiveChecker.GlobalForegroundTypeCheckCountStatistic
    (pF - pE) |> shouldEqual 0  // note, no new parse of the file
    (tF - tE) |> shouldEqual 0  // note, no new typecheck of the file
    backgroundParseCount.Value |> shouldEqual 10 // but note, the project does not get reparsed
    backgroundCheckCount.Value |> shouldEqual 7 // only two extra typechecks of files

    ()

