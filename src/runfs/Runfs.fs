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
    | RestoreError
    | BuildError

let ThisPackageName = "Runfs"
let DependenciesFileName = "dependencies.json"
let SourceHashFileName = "source.hash"
let ProjectName = "scriptCompile"
let ProjectFileName = ProjectName + ".fsproj"
let DllFileName = ProjectName + ".dll"
let NoRestore = false

let private GetTempPath(entryPointFullPath: string) =
    let SystemTempPath =
        // We want a location where permissions are expected to be restricted to the current user.
        let directory =
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                Path.GetTempPath()
            else
                Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData;

        Path.Join(directory, ThisPackageName);
    // Include entry point file name so the directory name is not completely opaque.
    let fileName = Path.GetFileNameWithoutExtension entryPointFullPath
    let hash = longhash (entryPointFullPath.ToLowerInvariant())
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
/// Directives:
///   move quote check/removal to regex?
///   add "checkArg" function argument to tryParse => finish refactoring
///   go back to C# compatible directives like #r_package
/// error handling: use railway for directives => finish dependencies
/// dont compute directives if no source change
/// show build output only when build errors or verbose (=> 2 versions of exec)
/// check build binlog for possible optimization
/// clean my repos
/// readme/ blog
/// fsharp/lang-design issue: parse and ignore #:
/// source, project refs
/// github actions
let run (options, entryPointSourcePathX, args) =
    let showTimings = Set.contains "time" options
    let verbose = Set.contains "verbose" options
    let noDependencyCheck = Set.contains "no-dependency-check" options
    let inline wrap name f = wrap showTimings name f

    result {
        let! sourcePath, projectDirectoryPath, projectFilePath, dependenciesHashPath, sourceHashPath, dllPath =
            wrap "creating paths" <| fun () -> result {
                do! File.Exists entryPointSourcePathX |> Result.requireTrue (InvalidSourcePath entryPointSourcePathX)

                let entryPointSourcePath = Path.GetFullPath entryPointSourcePathX
                let tempPath = GetTempPath entryPointSourcePath
                Directory.CreateDirectory tempPath |> ignore
                return
                    entryPointSourcePath,
                    tempPath,
                    Path.Join(tempPath, ProjectFileName),
                    Path.Join(tempPath, DependenciesFileName),
                    Path.Join(tempPath, SourceHashFileName),
                    Path.Join(tempPath, "artifacts/bin/debug", DllFileName)
            }

        let! sourceHash, directives = wrap "reading source and computing hash and directives" <| fun () -> result {
            let sourceLines = File.ReadAllLines sourcePath |> Array.toList
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
                let! sourceDir =
                    sourcePath |> Path.GetDirectoryName |> Result.requireNotNull (InvalidSourceDirectory sourcePath)
                return computeDependenciesHash (string sourceDir) directives
        }

        let! dependenciesChanged, sourceChanged = wrap "computing build level" <| fun () ->
            let dependenciesChanged =
                if noDependencyCheck then
                    false
                else
                    let readPreviousDependenciesHash() = File.ReadAllText dependenciesHashPath
                    not (File.Exists dependenciesHashPath && readPreviousDependenciesHash() = dependenciesHash)
            let sourceChanged =
                let readPreviousSourceHash() = File.ReadAllText sourceHashPath
                not (File.Exists sourceHashPath && readPreviousSourceHash() = sourceHash)
            Ok (dependenciesChanged, sourceChanged)
        
        if dependenciesChanged || sourceChanged then
            do! wrap "creating and writing project file" <| fun () ->
                let projectFileLines = createProjectFileLines directives sourcePath
                File.WriteAllLines(projectFilePath, projectFileLines) |> Ok

        if dependenciesChanged then
            do! wrap "running dotnet restore" <| fun () ->
                File.Delete dependenciesHashPath
                let exitCode = runCommand "dotnet" ["restore"; if not verbose then "-v:q"] projectDirectoryPath
                Result.requireEqual exitCode 0 RestoreError

        if sourceChanged || dependenciesChanged then
            do! wrap "running dotnet build" <| fun () ->
                let args = ["build"; "--no-restore"; "-consoleLoggerParameters:NoSummary"; if not verbose then "-v:q"]
                let exitCode = runCommand "dotnet" args projectDirectoryPath
                Result.requireEqual exitCode 0 BuildError

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
    GetTempPath "clear" |> Path.GetDirectoryName |> string |> deleteAll
    0