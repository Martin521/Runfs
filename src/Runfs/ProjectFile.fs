module Runfs.ProjectFile

open Runfs.Directives
open Runfs.Dependencies
open System.Security

/// Should be kept in sync with the default <c>dotnet new console</c>
let DefaultProperties = [
    "OutputType", "Exe"
    "TargetFramework", TargetFramework
    "SatelliteResourceLanguages", "en-US"   // t.b.d.
    "Nullable", "enable"                    // t.b.d.
    ]

let private escape str = SecurityElement.Escape str |> string

let private sdkLine project (name, version) =
    match version with
    | Some v -> $"""    <Import Project="{escape project}" Sdk="{escape name}" Version="{escape v}" />"""
    | None -> $"""    <Import Project="{escape project}" Sdk="{escape name}" />"""

let private propertyLine (name, version) =
    $"""        <{name}>{escape version}</{name}>"""

let private packageLine (name, version) =
    match version with
    | None -> $"""        <PackageReference Include="{escape name}" />"""
    | Some v -> $"""        <PackageReference Include="{escape name}" Version="{escape v}"/>"""

let createProjectFileLines directives entryPointSourceFullPath artifactsPath assemblyName =
    let sdks =
        match directives |> List.choose (function Sdk(n, v) -> Some(n, v) | _ -> None) with
        | [] -> ["Microsoft.NET.Sdk", None]  //DODO verison?
        | d -> d
    let properties =
        directives |> List.choose (function Property(n, v) -> Some(n.ToLowerInvariant(), v) | _ -> None) |> Map
    let remainingDefaultProperties =
        DefaultProperties |> List.filter (fun (k, _) -> not (Map.containsKey (k.ToLowerInvariant()) properties))
    let packages = directives |> List.choose (function Package(n, v) -> Some(n, v) | _ -> None)

    [
        "<Project>"
        "    <PropertyGroup>"
        $"""        <AssemblyName>{escape assemblyName}</AssemblyName>"""
        "        <UseArtifactsOutput>true</UseArtifactsOutput>"
        "        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>"
        $"""        <ArtifactsPath>{escape artifactsPath}</ArtifactsPath>"""
        "    </PropertyGroup>"
        yield! sdks |> List.map (sdkLine "Sdk.props")
        "    <PropertyGroup>"
        yield! remainingDefaultProperties |> List.map propertyLine
        yield! properties |> Map.toList |> List.map propertyLine
        "    </PropertyGroup>"
        "    <ItemGroup>"
        yield! packages |> List.map packageLine
        "    </ItemGroup>"
        "    <ItemGroup>"
        $"""        <Compile Include="{escape entryPointSourceFullPath}" />"""
        "    </ItemGroup>"
        yield! sdks |> List.map (sdkLine "Sdk.targets")
        "</Project>"
    ]