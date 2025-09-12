module Runfs.Test.DirectiveTests
open Xunit
open Runfs.Directives
open System.IO

let thisFileDirectory = __SOURCE_DIRECTORY__
let testFileDirectory = Path.Join(thisFileDirectory, "TestFiles")

let expectedDirectives = [
    // Project "abc/xyz.fsproj"
    // Dll "System.dll"
    Property ("myprop", "42")
    Property ("TargetFramework", "net8.0")
    Sdk ("Microsoft.Net.Sdk", Some "10.0.100")
    Sdk ("MySdk", None)
]

[<Fact>]
let ``Directives are correctly parsed`` () =
    let sourceFile = Path.Join(testFileDirectory, "DirectivesTestSource.fs")
    let sourceLines = File.ReadAllLines sourceFile |> Array.toList
    match getDirectives sourceLines with
    | Error _ -> Assert.Fail "Unexpected errors"
    | Ok directives ->
        Assert.Equal(directives.Length, expectedDirectives.Length)
        List.zip directives expectedDirectives |> List.iter Assert.Equal

let expectedErrors = [
    MissingPropertyValue(4, "myprop:42")
    MissingPropertyValue(5, "TargetFramework, net8.0")
    ]

[<Fact>]
let ``Incorrect directives create errors`` () =
    let sourceFile = Path.Join(testFileDirectory, "DirectivesErrorsSource.fs")
    let sourceLines = File.ReadAllLines sourceFile |> Array.toList
    match getDirectives sourceLines with
    | Error errors ->
        Assert.Equal(errors.Length, expectedErrors.Length)
        List.zip errors expectedErrors |> List.iter Assert.Equal
    | Ok directives ->
        Assert.Fail "Unexpected: no errors"

 