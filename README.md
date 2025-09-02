# Runfs

This is a demonstrator (work in progress), made to investigate if and how the "[dotnet run app.cs](https://devblogs.microsoft.com/dotnet/announcing-dotnet-run-app/)" functionality can be provided for F#.

You can use it to directly run an F# `.fs` source file.
So, if you want to build a small tool, you don't need to bother with projects and folders and artifacts any more. You rather just create your source file and run it with runfs. 

The source file can contain directives to reference libraries and to influence the build process, see section [Directives](#directives).

> Important: runfs is not about F# scripts (.fsx files) that can be run by `dotnet fsi`. See [below](#how-does-runfs-relate-to-fsi).

## Usage

```
dotnet tool install [-g] runfs
dotnet runfs app.fs
```
## Directives

The following directives (source lines) are recognized by runfs:

- `#r_package "pp@1.0.0"` - references package pp, version 1.0.0
- `#r_sdk "mysdk@0.0.1"` - references an sdk
- `#r_property "myprop=42"` - sets a build property
- `#r_project "xyz.fsproj"` - references a project [not yet implemented]
- `#r_dll "mylib.dll"` - references a library [not yet implemented]
- `#r_source "utilities.fs"` - references a source file [not yet implemented]

> The above directive syntax is preliminary and made to work with the current F# compiler (the parser accepts and ignores them). Ideally, the syntax defined for "dotnet run app.cs" should be reused, like `#:package pp@1.0.0`, but this needs a compiler fix first.

## How does runfs relate to fsi?

| | `dotnet runfs app.fs` | `dotnet fsi app.fsx` |
| --- | --- | --- |
| F# grammar | `implementationFile` grammar (full F# grammar) | `interaction` grammar (limited script grammar) |
| reference resolution | dotnet build | custom, dependence on the package tool |
| startup time initial run | 2 sec | 2 sec |
| startup time subsequent runs | 100 ms | 2 sec |
| conversion to F# project | easy (`dotnet runfs --convert app.fs`) | add namespaces / modules, create project file, <br/> move references to project file ... |
| availability | needs `dotnet tool install runfs` <br/> (until re-implemented into `dotnet run`) | available with sdk |
| maintainability | a small modular tool | a huge complex dinosaur |
| editor support | none yet | Ionide/FSAC, VS, Rider |

*(The timings are "order of magnitude" and depending on many factors)*

### fsi interactive

All of the above is not related to fsi interactive mode, which can be used in both cases with snippets from the source file ('alt Enter').

## Learnings

The main goal of this project is to investigate viability of `dotnet run app.fs` and to create clarity on requirements and missing pieces.

I started by looking into extending the sdk code to accomodate F# input. This would have meant, however, to work with some particularly ugly (in my eyes) C# code. So, I chose to create an independent tool for the investigation, reproducing the functionality as closely as possible.

Main learnings
- This has to be about running .fs files, not scripts.
- A small compiler change is needed to allow for the directive format.
- The most important missing piece is editor support.

## TODOs

Runfs
- investigate if the "compile only" shortcut can be replicated for F#
- add more tests, possibly Rid-package, fix case sensitivity issue in Directives.fs
- implement the #project, #source and #dll directives and `--convert`

Elsewhere
- propose and possibly implement a compiler change so that the 'dotnet run app.cs' syntax for directives can be used
- Ionide/FSAC support
- collect feedback for an eventual `dotnet run app.fs`

