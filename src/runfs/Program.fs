module Runfs.Main

open Runfs.Runfs
open Runfs.Directives

[<EntryPoint>]
let main argv =

    let argv = Array.toList argv

    match argv with
    | ["--clear"] -> clearCaches ()
    | _ ->

    let getArgs argv =
        let rec getargs (argv: string list) options =
            match argv with
            | [] -> None
            | "--time"::t -> getargs t (Set.add "time" options)
            | "--verbose"::t -> getargs t (Set.add "verbose" options)
            | "--no-dependency-check"::t -> getargs t (Set.add "no-dependency-check" options)
            | h::t -> Some (options, h, t)
        getargs argv Set.empty

    let errorExitCode = 42
    match getArgs argv with
    | None ->
        printfn "usage: runfs [--time] [--verbose] [--no-dependency-check] source args OR runfs --clear"
        errorExitCode
    | Some args ->
        match run args with
        | Ok exitCode -> exitCode
        | Error err ->
            let errorStrings =
                match err with
                | CaughtException ex -> [$"Unexpected: {ex.Message}"]
                | InvalidSourcePath s -> [$"Invalid source path: {s}"]
                | InvalidSourceDirectory s -> [$"Invalid source directory: {s}"]
                | RestoreError -> [$"Restore error"]
                | BuildError -> [$"Build error"]
                | DirectiveError parseErrors ->
                    let getParseErrorString parseError =
                        match parseError with
                        | UnknownKind(n, kind) -> $"Line {n} Unknown Runfs directive {kind}"
                        | MissingArgument n -> $"Line {n}: Missing directive argument"
                        | ArgumentNotQuoted(n, s) -> $"Line {n}: Directive arguments must be quoted (for now)"
                        | InvalidPropertyName(n, s) -> $"Line {n}: Invalid property name"
                        | MissingPropertyValue(n, s) -> $"Line {n}: Missing property value"
                    parseErrors |> List.map getParseErrorString
            printfn $"{ThisPackageName}: {errorStrings.Length} stopped with error(s):"
            errorStrings |> List.iter (printfn "  %s")
            errorExitCode
