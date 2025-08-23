open System

#r_project "abc/xyz.fsproj"
#r_dll "System.dll"
#r_property "myprop=42"
#r_property "TargetFramework=net8.0"

let args = System.Environment.GetCommandLineArgs() |> Array.toList |> List.tail
// printfn "hi!"
printfn $"args: {args}" 
// Threading.Thread.Sleep 2000
// printfn "ho."
// Console.Error.WriteLine "this is error output" 



