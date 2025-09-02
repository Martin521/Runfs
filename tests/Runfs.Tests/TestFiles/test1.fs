module A =
    open System

#r_project "abc/xyz.fsproj"
#r_dll "System.dll"
#r_property "myprop=43"
#r_property "TargetFramework=net9.0"

open System

let args = System.Environment.GetCommandLineArgs() |> Array.toList |> List.tail
printfn $"args: {args}" 

// let RuntimeVersion = Environment.Version
// printfn $"Runtime version: {RuntimeVersion}"
