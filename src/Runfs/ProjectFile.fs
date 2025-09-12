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

let private sourceLine name =
    $"""        <Compile Include="{escape name}" />"""

let private dllLine name =
    $"""        <Reference Include="{escape name}" />"""

let private projectLine name =
    $"""        <ProjectReference Include="{escape name}" />"""

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
    let dlls = directives |> List.choose (function Dll n -> Some n | _ -> None)
    let sources = directives |> List.choose (function Source n -> Some n | _ -> None)
    let projects = directives |> List.choose (function Project n -> Some n | _ -> None)

    [
        "<Project>"
        "    <PropertyGroup>"
        $"""        <AssemblyName>{escape assemblyName}</AssemblyName>"""
        "        <UseArtifactsOutput>true</UseArtifactsOutput>"
        "        <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>"
        $"""        <ArtifactsPath>{escape artifactsPath}</ArtifactsPath>"""
        $"""        <FileBasedProgram>true</FileBasedProgram>"""
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
        yield! dlls |> List.map dllLine
        "    </ItemGroup>"
        "    <ItemGroup>"
        yield! projects |> List.map projectLine
        "    </ItemGroup>"
        "    <ItemGroup>"
        yield! sources |> List.map sourceLine
        $"""        <Compile Include="{escape entryPointSourceFullPath}" />"""
        "    </ItemGroup>"
        yield! sdks |> List.map (sdkLine "Sdk.targets")

        $""" <!-- Override targets which don't work with project files that are not present on disk. --> """
        $""" <!-- Hopefully we can remove this once net10 has landed. --> """

        $""" <Target Name="_FilterRestoreGraphProjectInputItems" """
        $"""         DependsOnTargets="_LoadRestoreGraphEntryPoints" """
        $"""         Returns="@(FilteredRestoreGraphProjectInputItems)"> """
        $"""     <ItemGroup> """
        $"""         <FilteredRestoreGraphProjectInputItems Include="@(RestoreGraphProjectInputItems)" /> """
        $"""     </ItemGroup> """
        $""" </Target> """

        $""" <Target Name="_GetAllRestoreProjectPathItems" """
        $"""         DependsOnTargets="_FilterRestoreGraphProjectInputItems" """
        $"""         Returns="@(_RestoreProjectPathItems)"> """
        $"""     <ItemGroup> """
        $"""         <_RestoreProjectPathItems Include="@(FilteredRestoreGraphProjectInputItems)" /> """
        $"""     </ItemGroup> """
        $""" </Target> """

        $""" <Target Name="_GenerateRestoreGraph" """
        $"""         DependsOnTargets="_FilterRestoreGraphProjectInputItems;_GetAllRestoreProjectPathItems;_GenerateRestoreGraphProjectEntry;_GenerateProjectRestoreGraph" """
        $"""         Returns="@(_RestoreGraphEntry)"> """
        $"""     <!-- Output from dependency _GenerateRestoreGraphProjectEntry and _GenerateProjectRestoreGraph --> """
        $""" </Target> """

        "</Project>"
    ]