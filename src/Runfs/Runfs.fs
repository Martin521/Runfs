module Runfs.Runfs

open System
open System.IO
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open Runfs.Directives
open Runfs.ProjectFile
open Runfs.Dependencies
open Runfs.Utilities

type RunfsError =
    | CaughtException of Exception
    | InvalidSourcePath of string
    | InvalidSourceDirectory of string
    | DirectiveError of ParseError list
    | RestoreError of stdout: string list * stderr: string list
    | BuildError of stdout: string list * stderr: string list

let ThisPackageName = "Runfs"
let DependenciesFileName = "dependencies.json"
let SourceHashFileName = "source.hash"
let ProjectName = $"__{ThisPackageName}__"
let ProjectFileName = ProjectName + ".fsproj"
let AssemblyName = ThisPackageName
let DllFileName = AssemblyName + ".dll"
let NoRestore = false

let private GetArtifactsPath(fullSourcePath: string) =
    let SystemTempPath =
        // We want a location where permissions are expected to be restricted to the current user.
        let directory =
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                Path.GetTempPath()
            else
                Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData;

        Path.Join(directory, ThisPackageName);
    // Include entry point file name so the directory name is not completely opaque.
    let fileName = Path.GetFileNameWithoutExtension fullSourcePath
    let hash = longhash fullSourcePath
    let directoryName = $"{fileName}-{hash}"
    Path.Join(SystemTempPath, directoryName)

/// Capture exceptions as Errors and (if requested) measure and print timing
let wrap showTimings name f =
    try
        if showTimings then
            let startTime = DateTime.Now
            let result = f()
            printfn $"{ThisPackageName}: %4.0f{(DateTime.Now - startTime).TotalMilliseconds}ms for {name}"
            result
        else f()
    with ex -> Error (CaughtException ex)


/// TODO
/// virtual project file
/// clean my repos
/// fsharp/lang-design issue: parse and ignore #:
/// readme/ blog
///    also add TODOS
///         check build binlog for possible optimization (core compile instead of build target)
///         e2e tests
///         source, project refs
///         release action
let run (options, sourcePath, args) =
    let showTimings = Set.contains "time" options
    let verbose = Set.contains "verbose" options
    let noDependencyCheck = Set.contains "no-dependency-check" options
    let inline wrap name f = wrap showTimings name f

    result {
        let! fullSourcePath, fullSourceDir, artifactsDir, projectFilePath, dependenciesHashPath, sourceHashPath, dllPath =
            wrap "creating paths" <| fun () -> result {
                do! File.Exists sourcePath |> Result.requireTrue (InvalidSourcePath sourcePath)

                let fullSourcePath = Path.GetFullPath sourcePath
                let! fullSourceDir =
                    fullSourcePath
                    |> Path.GetDirectoryName
                    |> Result.requireNotNull (InvalidSourceDirectory fullSourcePath)
                    |> Result.map string
                let artifactsDir = GetArtifactsPath fullSourcePath
                Directory.CreateDirectory artifactsDir |> ignore
                return
                    fullSourcePath,
                    fullSourceDir,
                    artifactsDir,
                    Path.Join(fullSourceDir, ProjectFileName),
                    Path.Join(artifactsDir, DependenciesFileName),
                    Path.Join(artifactsDir, SourceHashFileName),
                    Path.Join(artifactsDir, "bin\\debug", DllFileName)
            }

        let! sourceHash, directives = wrap "reading source and computing hash and directives" <| fun () -> result {
            let sourceLines = File.ReadAllLines fullSourcePath |> Array.toList
            let sourceHash = sourceLines |> String.concat "\n" |> longhash
            let directives = getDirectives sourceLines |> Result.mapError DirectiveError
            let! both = directives |> Result.map (fun ds -> sourceHash, ds)
            return both
        }
        
        if verbose then
            printfn "The following directives were found"
            directives |> List.iter (printfn "  %A")

        let! dependenciesHash = wrap "computing dependency hash" <| fun () -> result {
            if noDependencyCheck then
                return ""
            else
                let! sourceDir =    //TODO: move up
                    fullSourcePath |> Path.GetDirectoryName |> Result.requireNotNull (InvalidSourceDirectory fullSourcePath)
                return computeDependenciesHash (string sourceDir) directives
        }

        let! dependenciesChanged, sourceChanged, noDll = wrap "computing build level" <| fun () ->
            let dependenciesChanged =
                if noDependencyCheck then
                    false
                else
                    let readPreviousDependenciesHash() = File.ReadAllText dependenciesHashPath
                    not (File.Exists dependenciesHashPath && readPreviousDependenciesHash() = dependenciesHash)
            let sourceChanged =
                let readPreviousSourceHash() = File.ReadAllText sourceHashPath
                not (File.Exists sourceHashPath && readPreviousSourceHash() = sourceHash)
            let noDll = not (File.Exists dllPath)
            Ok (dependenciesChanged, sourceChanged, noDll)
        
        if dependenciesChanged || sourceChanged || noDll then
            do! wrap "creating and writing project file" <| fun () ->
                let projectFileLines = createProjectFileLines directives fullSourcePath artifactsDir AssemblyName
                File.WriteAllLines(projectFilePath, projectFileLines) |> Ok

        if dependenciesChanged || noDll then
            do! wrap "running dotnet restore" <| fun () ->
                File.Delete dependenciesHashPath
                let args = [
                    "restore"
                    if not verbose then "-v:q"
                    projectFilePath
                    ]
                let exitCode, stdoutLines, stderrLines =
                    runCommandCollectOutput "dotnet" args fullSourceDir
                if exitCode <> 0 then Error(RestoreError(stdoutLines, stderrLines)) else Ok()

        if sourceChanged || dependenciesChanged || noDll then
            do! wrap "running dotnet build" <| fun () ->
                let args = [
                    "build"
                    "--no-restore"
                    "-consoleLoggerParameters:NoSummary"
                    if not verbose then "-v:q"
                    projectFilePath
                ]
                let exitCode, stdoutLines, stderrLines =
                    runCommandCollectOutput "dotnet" args fullSourceDir
                if exitCode <> 0 then Error(BuildError(stdoutLines, stderrLines)) else Ok()

        if dependenciesChanged then
            do! wrap "saving dependencies hash" <| fun () ->
                File.WriteAllText(dependenciesHashPath, dependenciesHash) |> Ok

        if sourceChanged then
            do! wrap "saving source hash" <| fun () ->
                File.WriteAllText(sourceHashPath, sourceHash) |> Ok

        let! exitCode = wrap "executing program" <| fun () ->
            runCommand "dotnet" (dllPath::args) "." |> Ok
        
        return exitCode
    }

let clearCaches () =
    let rec deleteAll path =
        for file in Directory.GetFiles path do File.Delete file
        for dir in Directory.GetDirectories path do deleteAll dir; Directory.Delete dir
    GetArtifactsPath "clear" |> Path.GetDirectoryName |> string |> deleteAll
    0