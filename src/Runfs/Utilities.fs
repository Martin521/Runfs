module Runfs.Utilities

open System
open System.Diagnostics
open System.Security.Cryptography
open System.Text

let longhash (text: string) =
    let bytes = Encoding.UTF8.GetBytes text;
    let hash = SHA256.HashData bytes;
    Convert.ToHexStringLower hash;

let runCommand executable (args: string list) workingDirectory =
    let processInfo = new ProcessStartInfo(executable, args)
    processInfo.WorkingDirectory <- workingDirectory
    processInfo.CreateNoWindow <- true
    processInfo.UseShellExecute <- false
    processInfo.RedirectStandardError <- true
    processInfo.RedirectStandardOutput <- true
    use p = new Process()
    p.StartInfo <- processInfo
    p.OutputDataReceived.Add (fun ea -> if ea.Data <> null then Console.WriteLine ea.Data)
    p.ErrorDataReceived.Add (fun ea -> if ea.Data <> null then Console.Error.WriteLine ea.Data)
    p.Start() |> ignore
    p.BeginOutputReadLine()
    p.BeginErrorReadLine()
    p.WaitForExit()
    p.ExitCode

let runCommandCollectOutput executable (args: string list) workingDirectory =
    let processInfo = ProcessStartInfo(executable, args)
    processInfo.WorkingDirectory <- workingDirectory
    processInfo.CreateNoWindow <- true
    processInfo.UseShellExecute <- false
    processInfo.RedirectStandardError <- true
    processInfo.RedirectStandardOutput <- true
    let mutable stdoutLines = []
    let mutable stderrLines = []
    use p = new Process()
    p.StartInfo <- processInfo
    p.OutputDataReceived.Add (fun ea -> if ea.Data <> null then stdoutLines <- ea.Data::stdoutLines)
    p.ErrorDataReceived.Add (fun ea -> if ea.Data <> null then stderrLines <- ea.Data::stderrLines)
    p.Start() |> ignore
    p.BeginOutputReadLine()
    p.BeginErrorReadLine()
    p.WaitForExit()
    p.ExitCode, List.rev stdoutLines, List.rev stderrLines

