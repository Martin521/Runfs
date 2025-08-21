module Runfs.Runfs

open System
open System.IO
open System.Runtime.InteropServices
open Runfs.Directives
open Runfs.ProjectFile
open Runfs.Dependencies
open Runfs.Utilities

type RunfsError =
    | CaughtException of Exception
    | Other

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

let wrap showTimings name f =
    try
        if showTimings then
            let startTime = DateTime.Now
            let result = f()
            printfn $"{ThisPackageName}: %4.0f{(DateTime.Now - startTime).TotalMilliseconds}ms ({name})"
            result
        else f()
    with ex -> Error (CaughtException ex)


/// TODO
/// --clear
/// error handling: use railway for directives, dependencies
/// show build output only when build errors or verbose
/// check build binlog for possible optimization
/// readme/ blog
/// source, project refs
let run (options, entryPointSourcePath, args) =
    let startTime = DateTime.Now
    let showTime = Set.contains "time" options
    let verbose = Set.contains "verbose" options
    let noDependencyCheck = Set.contains "no-dependency-check" options
    let inline showTime where =
        if showTime then printfn $"{ThisPackageName}: %6.0f{(DateTime.Now - startTime).TotalMilliseconds}ms: ({where})"
    showTime "start"

    if not (File.Exists entryPointSourcePath) then
        printfn $"source {entryPointSourcePath} does not exist"
        1
    else

    let entryPointSourcePath = Path.GetFullPath entryPointSourcePath
    let tempPath = GetTempPath entryPointSourcePath
    Directory.CreateDirectory tempPath |> ignore
    showTime "temp dir found / created"
    
    let projectFilePath = Path.Join(tempPath, ProjectFileName)
    let dependenciesHashPath = Path.Join(tempPath, DependenciesFileName)
    let sourceHashPath = Path.Join(tempPath, SourceHashFileName)
    let dllPath = Path.Join(tempPath, "artifacts/bin/debug", DllFileName)
    let sourceLines = File.ReadAllLines entryPointSourcePath |> Array.toList
    let sourceHash = sourceLines |> String.Concat |> longhash
    showTime "source read and hash created"
    
    let directives = getDirectives sourceLines
    if verbose then
        printfn "The following directives were found"
        directives |> List.iter (printfn "  %A")
    showTime "directives extracted"
    
    let dependenciesHash =
        if noDependencyCheck then "" else computeDependenciesHash entryPointSourcePath directives
    showTime "dependency hash computed"

    let dependenciesChanged =
        if noDependencyCheck then
            false
        else
            let readPreviousDependenciesHash() = File.ReadAllText dependenciesHashPath
            not (File.Exists dependenciesHashPath && readPreviousDependenciesHash() = dependenciesHash)
    let sourceChanged =
        let readPreviousSourceHash() = File.ReadAllText sourceHashPath
        not (File.Exists sourceHashPath && readPreviousSourceHash() = sourceHash)
    showTime "build level computed"
    
    if dependenciesChanged || sourceChanged then
        let projectFileLines = createProjectFileLines directives entryPointSourcePath
        File.WriteAllLines(projectFilePath, projectFileLines)
        showTime "temp project created"

    if dependenciesChanged then
        File.Delete dependenciesHashPath
        runCommand "dotnet" ["restore"; if not verbose then "-v:q"] tempPath
        showTime "restore completed"

    if sourceChanged || dependenciesChanged then
        runCommand "dotnet" ["build"; "--no-restore"; "-consoleLoggerParameters:NoSummary"; if not verbose then "-v:q"] tempPath
        showTime "build completed"

    if dependenciesChanged then
        File.WriteAllText(dependenciesHashPath, dependenciesHash)
    if sourceChanged then
        File.WriteAllText(sourceHashPath, sourceHash)
    showTime "hashes written, starting your program"

    runCommand "dotnet" (dllPath::args) "."
    showTime "run completed"
    0

let clearCaches () =
    let rec deleteAll path =
        for file in Directory.GetFiles path do File.Delete file
        for dir in Directory.GetDirectories path do deleteAll dir; Directory.Delete dir
    GetTempPath "clear" |> Path.GetDirectoryName |> string |> deleteAll
    0