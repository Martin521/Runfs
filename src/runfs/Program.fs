module Runfs.Main

open Runfs.Runfs

[<EntryPoint>]
let main argv =
    let getArgs argv =
        let rec getargs (argv: string list) options =
            match argv with
            | [] -> None
            | "--verbose"::t -> getargs t (Set.add "verbose" options)
            | "--no-dependency-check"::t -> getargs t (Set.add "no-dependency-check" options)
            | h::t -> Some (options, h, t)
        getargs argv Set.empty

    match getArgs (Array.toList argv) with
    | None -> printfn "usage: runfs [--verbose] [--no-dependency-check] source args"; 1
    | Some args -> run args
