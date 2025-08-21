module Runfs.Main

open Runfs.Runfs

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

    match getArgs argv with
    | None -> printfn "usage: runfs [--time] [--verbose] [--no-dependency-check] source args"; 1
    | Some args ->
        try run args
        with ex -> printfn $"Fatal error: {ex.Message}"; 1
