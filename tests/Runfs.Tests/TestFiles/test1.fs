open System

#r_project "abc/xyz.fsproj"
#r_dll "System.dll"
#r_property "myprop=43"
#r_property "TargetFramework=net8.0"

let args = System.Environment.GetCommandLineArgs() |> Array.toList |> List.tail
printfn $"args: {args}" 

