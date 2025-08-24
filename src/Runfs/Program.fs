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
        | Ok (exitCode, _, _) -> exitCode
        | Error err ->
            let indent lines = lines |> List.map (fun s -> "    " + s)
            let errorStrings =
                match err with
                | CaughtException ex -> [$"Unexpected: {ex.Message}"]
                | InvalidSourcePath s -> [$"Invalid source path: {s}"]
                | InvalidSourceDirectory s -> [$"Invalid source directory: {s}"]
                | RestoreError(stdoutLines, stderrLines) ->
                    "Restore error" :: indent stdoutLines @ indent stderrLines
                | BuildError(stdoutLines, stderrLines) ->
                    "Build error" :: indent stdoutLines @ indent stderrLines
                | DirectiveError parseErrors ->
                    let getParseErrorString parseError =
                        let prefix n = $"  Line %3d{n}: "
                        match parseError with
                        | UnknownKind(n, kind) -> $"{prefix n}Unknown Runfs directive {kind}"
                        | MissingArgument n -> $"{prefix n}Missing directive argument"
                        | ArgumentNotQuoted(n, s) -> $"{prefix n}Directive arguments must be quoted (for now)"
                        | InvalidPropertyName(n, s) -> $"{prefix n}Invalid property name"
                        | MissingPropertyValue(n, s) -> $"{prefix n}Missing property value"
                    parseErrors |> List.map getParseErrorString
            printfn $"{ThisPackageName}: stopped with error(s):"
            errorStrings |> List.iter (printfn "  %s")
            errorExitCode
