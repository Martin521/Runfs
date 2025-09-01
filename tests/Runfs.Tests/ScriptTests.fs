module Runfs.Test.ScriptTests

open System.IO
open Xunit
open Runfs.Utilities
open Runfs.Runfs

let thisFileDirectory = __SOURCE_DIRECTORY__
let testFile1 = Path.Join(thisFileDirectory, "TestFiles/test1.fs")
let testFile2 = Path.Join(thisFileDirectory, "TestFiles/test2.fs")

let configPath =
    #if DEBUG
        "debug"
    #else
        "release"
    #endif
let runfsDll = Path.Join(thisFileDirectory, $"../../artifacts/bin/runfs/{configPath}/Runfs.dll")

[<Fact>]
let ``found runfs executable`` () =
    Assert.True(File.Exists runfsDll, $"not found: {runfsDll}")

[<Fact>]
let ``testFile1 runs correctly`` () =
    let exitCode, outLines, errLines = runCommandCollectOutput "dotnet" [runfsDll; testFile1; "a"; "b"] thisFileDirectory
    Assert.Equal(0, exitCode)
    Assert.Equal("args: [a; b]", List.exactlyOne outLines)
    Assert.Empty errLines

[<Fact>]
let ``testFile2 runs correctly`` () =
    let exitCode, outLines, errLines = runCommandCollectOutput "dotnet" [runfsDll; testFile2] thisFileDirectory
    Assert.Equal(0, exitCode)
    Assert.Equal("""{"x":"Hello","y":"world!"}""", List.exactlyOne outLines)
    Assert.Empty errLines

 