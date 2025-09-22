module Runfs.Runfs

open System
open System.IO
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open Runfs.Directives
open Runfs.ProjectFile
open Runfs.Dependencies
open Runfs.Utilities
open Runfs.Build

type RunfsError =
    | CaughtException of Exception
    | InvalidSourcePath of string
    | InvalidSourceDirectory of string
    | DirectiveError of ParseError list
    | BuildError of MSBuildError

let ThisPackageName = "Runfs"
let DependenciesHashFileName = "dependencies.hash"
let SourceHashFileName = "source.hash"
let ProjectName = $"virtual_runfs_project"
let ProjectFileName = ProjectName + ".fsproj"
let AssemblyName = ThisPackageName
let DllFileName = AssemblyName + ".dll"
let NoRestore = false

let private getArtifactsPath(fullSourcePath: string) =
    let SystemTempPath =
        // We want a location where permissions are expected to be restricted to the current user.
        let directory =
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                Path.GetTempPath()
            else
                Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData;

        Path.Join(directory, ThisPackageName);
    // Include source file name so the directory name is not completely opaque.
    let fileName = Path.GetFileNameWithoutExtension fullSourcePath
    let hash = longhash fullSourcePath
    let directoryName = $"{fileName}-{hash}"
    Path.Join(SystemTempPath, directoryName)

/// Capture exceptions as Errors and (if requested) measure and print timing
let guardAndTime showTimings name f =
    try
        if showTimings then
            let startTime = DateTime.Now
            let result = f()
            printfn $"{ThisPackageName}: %4.0f{(DateTime.Now - startTime).TotalMilliseconds}ms for {name}"
            result
        else f()
    with ex -> Error (CaughtException ex)


let run (options, sourcePath, args) =
    let showTimings = Set.contains "time" options
    let verbose = Set.contains "verbose" options
    let noDependencyCheck = Set.contains "no-dependency-check" options
    let inline guardAndTime name f = guardAndTime showTimings name f

    initMSBuild()

    result {
        let! fullSourcePath, fullSourceDir, artifactsDir, virtualProjectFilePath,
            savedProjectFilePath, dependenciesHashPath, sourceHashPath, dllPath =
                guardAndTime "creating paths" <| fun () -> result {
                    do! File.Exists sourcePath |> Result.requireTrue (InvalidSourcePath sourcePath)

                    let fullSourcePath = Path.GetFullPath sourcePath
                    let! fullSourceDir =
                        fullSourcePath
                        |> Path.GetDirectoryName
                        |> Result.requireNotNull (InvalidSourceDirectory fullSourcePath)
                        |> Result.map string
                    let artifactsDir = getArtifactsPath fullSourcePath
                    Directory.CreateDirectory artifactsDir |> ignore
                    return
                        fullSourcePath,
                        fullSourceDir,
                        artifactsDir,
                        Path.Join(fullSourceDir, ProjectFileName),
                        Path.Join(artifactsDir, ProjectFileName),
                        Path.Join(artifactsDir, DependenciesHashFileName),
                        Path.Join(artifactsDir, SourceHashFileName),
                        Path.Join(artifactsDir, "bin", "debug", DllFileName)
                }

        let! sourceHash, directives =
            guardAndTime "reading source and computing hash and directives" <| fun () -> result {
                let sourceLines = File.ReadAllLines fullSourcePath |> Array.toList
                let sourceHash = sourceLines |> String.concat "\n" |> longhash
                let directives = getDirectives sourceLines |> Result.mapError DirectiveError
                let! both = directives |> Result.map (fun ds -> sourceHash, ds)
                return both
            }
        
        if verbose then
            printfn "The following directives were found"
            directives |> List.iter (printfn "  %A")

        let! dependenciesHash = guardAndTime "computing dependency hash" <| fun () -> result {
            if noDependencyCheck then
                return ""
            else
                return computeDependenciesHash (string fullSourceDir) directives
        }

        let! needsRestore, needsBuild = guardAndTime "computing build level" <| fun () ->
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
            let hasProjectDirective = directives |> List.exists (function Project _ -> true | _ -> false)
            Ok (dependenciesChanged || noDll || hasProjectDirective, sourceChanged)
        
        if needsRestore then
            do! guardAndTime "creating and writing project file" <| fun () ->
                let projectFileLines = createProjectFileLines directives fullSourcePath artifactsDir AssemblyName
                File.WriteAllLines(savedProjectFilePath, projectFileLines) |> Ok
        
        if needsRestore || needsBuild then
            use! project = guardAndTime "creating msbuild project instance" <| fun () ->
                let projectFileText = File.ReadAllText savedProjectFilePath
                createProject verbose virtualProjectFilePath projectFileText |> Ok
        
            if needsRestore then
                do! guardAndTime "running msbuild restore" <| fun () -> result {
                    File.Delete dependenciesHashPath
                    do! build "restore" project |> Result.mapError BuildError
                }
            
            do! guardAndTime "running dotnet build" <| fun () -> result {
                File.Delete sourceHash
                do! build "build" project |> Result.mapError BuildError
            }

            if needsRestore then
                do! guardAndTime "saving dependencies hash" <| fun () ->
                    File.WriteAllText(dependenciesHashPath, dependenciesHash) |> Ok

            if needsBuild then
                do! guardAndTime "saving source hash" <| fun () ->
                    File.WriteAllText(sourceHashPath, sourceHash) |> Ok

        let! exitCode = guardAndTime "executing program" <| fun () ->
            runCommand "dotnet" (dllPath::args) "." |> Ok
        
        return exitCode
    }

let clearCaches () =
    let rec deleteAll path =
        for file in Directory.GetFiles path do File.Delete file
        for dir in Directory.GetDirectories path do deleteAll dir; Directory.Delete dir
    getArtifactsPath "clear" |> Path.GetDirectoryName |> string |> deleteAll
    0