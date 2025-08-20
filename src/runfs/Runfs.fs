module Runfs.Runfs

open System
open System.IO
open System.Runtime.InteropServices
open Runfs.Directives
open Runfs.ProjectFile
open Runfs.Dependencies
open Runfs.Utilities

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

/// TODO
/// check build binlog for possible optimization
/// error handling
/// package
/// blog
let run (options, entryPointSourcePath, args) =
    let startTime = DateTime.Now
    let verbose = Set.contains "verbose" options
    let noDependencyCheck = Set.contains "no-dependency-check" options
    let inline showTime where =
        if verbose then printfn $"{ThisPackageName}: %6.0f{(DateTime.Now - startTime).TotalMilliseconds}ms: ({where})"
    showTime "start"
    let entryPointSourcePath = Path.GetFullPath entryPointSourcePath
    let tempPath = GetTempPath entryPointSourcePath
    Directory.CreateDirectory tempPath |> ignore
    showTime "temp dir found / created"
    let projectFilePath = Path.Join(tempPath, ProjectFileName)
    let dependenciesPath = Path.Join(tempPath, DependenciesFileName)
    let sourceHashPath = Path.Join(tempPath, SourceHashFileName)
    let dllPath = Path.Join(tempPath, "artifacts/bin/debug", DllFileName)
    let sourceLines = File.ReadAllLines entryPointSourcePath |> Array.toList
    let sourceHash = sourceLines |> String.Concat |> longhash
    showTime "source read and hash created"
    let directives = getDirectives sourceLines
    showTime "directives extracted"
    let dependenciesHash =
        if noDependencyCheck then "" else computeDependenciesHash entryPointSourcePath directives
    showTime "dependency hash computed"

    let dependenciesChanged =
        if noDependencyCheck then
            false
        else
            let readPreviousDependenciesHash() = File.ReadAllText dependenciesPath
            not (File.Exists dependenciesPath && readPreviousDependenciesHash() = dependenciesHash)
    let sourceChanged =
        let readPreviousSourceHash() = File.ReadAllText sourceHashPath
        not (File.Exists sourceHashPath && readPreviousSourceHash() = sourceHash)
    showTime "build level computed"
    
    if dependenciesChanged || sourceChanged then
        let projectFileLines = createProjectFileLines directives entryPointSourcePath
        File.WriteAllLines(projectFilePath, projectFileLines)
        showTime "temp project created"
    if dependenciesChanged then
        File.Delete dependenciesPath
        runCommand "dotnet" ["restore"] tempPath
        showTime "restore completed"
    if sourceChanged || dependenciesChanged then
        runCommand "dotnet" ["build"; "--no-restore"] tempPath
        showTime "build completed"
    if dependenciesChanged then
        File.WriteAllText(dependenciesPath, dependenciesHash)
    if sourceChanged then
        File.WriteAllText(sourceHashPath, sourceHash)
    showTime "hashes written, starting your program"
    runCommand "dotnet" (dllPath::args) "."
    showTime "run completed"
    0
