module Runfs.Test.Simple

open System
open System.IO
open Xunit
open Runfs.Runfs

let thisFileDirectory = __SOURCE_DIRECTORY__
let testFile1 = Path.Join(thisFileDirectory, "TestFiles/test1.fs")

[<Fact>]
let ``Simple program runs correctly`` () =
    match run (Set["with-output"], testFile1, ["a"; "b"]) with
    | Error err -> Assert.Fail $"unexpected error {err}"
    | Ok (exitCode, outLines, errLines) ->
        Assert.Equal(0, exitCode)
        Assert.True(("args: [a; b]" = List.exactlyOne outLines))
        Assert.Empty errLines

 