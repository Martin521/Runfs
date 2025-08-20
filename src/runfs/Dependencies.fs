module Runfs.Dependencies

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open Runfs.Directives
open Runfs.Utilities

let RuntimeVersion = Environment.Version
let SdkVersion = Runtime.InteropServices.RuntimeInformation.FrameworkDescription
let TargetFramework = $"net{RuntimeVersion.Major}.{RuntimeVersion.Minor}"

let PotentialImplicitBuildFileNames = [
    "Directory.Build.props"
    "Directory.Build.targets"
    "Directory.Packages.props"
    "Directory.Build.rsp"
    "MSBuild.rsp"
    "global.json"

    // All these casings are recognized on case-sensitive platforms:
    // https://github.com/NuGet/NuGet.Client/blob/ab6b96fd9ba07ed3bf629ee389799ca4fb9a20fb/src/NuGet.Core/NuGet.Configuration/Settings/Settings.cs#L32-L37
    "nuget.config"
    "NuGet.config"
    "NuGet.Config"
    ]

type private ImplicitDependencies = {
    // TODO: deal with comparer for ImplicitBuildFiles
    ImplicitBuildFilesWithModTime: (string * DateTime) list
    SdkVersion: string
    RuntimeVersion: string
}

type private Dependencies = {
    ImplicitDependencies: ImplicitDependencies
    Directives: Directive list
}

let rec private findImplicitBuildFileNames (directory: DirectoryInfo) fileNameList =
    let getFullName fileName = Path.Join(directory.FullName, fileName)
    let found = fileNameList |> List.filter (getFullName >> File.Exists)
    match directory.Parent with
    | null -> found
    | parent -> found @ findImplicitBuildFileNames parent fileNameList

let private collectImplicitDependencies (entryPointSourcePath: string) =
    let entryPointFile = FileInfo entryPointSourcePath
    let directory: DirectoryInfo = match entryPointFile.Directory with null -> failwith "ddd" | d -> d
    let implicitBuildFiles = findImplicitBuildFileNames directory PotentialImplicitBuildFileNames
    let ibfWithModTimes = implicitBuildFiles |> List.map (fun f -> f, File.GetLastWriteTimeUtc f)
    {
        ImplicitBuildFilesWithModTime = ibfWithModTimes
        SdkVersion = SdkVersion
        RuntimeVersion = string RuntimeVersion
    }

let computeDependenciesHash entryPointSourcePath directives =
    let dependencies = {
        ImplicitDependencies = collectImplicitDependencies entryPointSourcePath
        Directives = directives
    }
    let options = JsonFSharpOptions.Default().ToJsonSerializerOptions()
    JsonSerializer.Serialize(dependencies, options) |> longhash
