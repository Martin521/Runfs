#r_package "FSharp.SystemTextJson@1.4.36"
//#r_sdk "Microsoft.Net.Sdk"

open System.Text.Json
open System.Text.Json.Serialization

let options = JsonFSharpOptions.Default().ToJsonSerializerOptions()

let s = JsonSerializer.Serialize({| x = "Hello"; y = "world!" |}, options)
printfn $"%s{s}" 
