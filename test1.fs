open System

#project "abc/xyz.fsproj"
#binary "System.dll"
#property "myprop=42"
#property "TargetFramework=net8.0"

let args = System.Environment.GetCommandLineArgs() |> Array.toList |> List.tail
// printfn "hi!"
printfn $"args: {args}" 
// Threading.Thread.Sleep 2000
// printfn "ho."
// Console.Error.WriteLine "this is error output" 
x