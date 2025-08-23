namespace Unknown
#r_project "abc/xyz.fsproj"
#r_dll "System.dll"
#r_property "myprop=42"
#r_property "TargetFramework=net8.0"
#r_sdk "Microsoft.Net.Sdk, 10.0.100"
#r_sdk "MySdk"
#nowarn 25
printfn "hello"