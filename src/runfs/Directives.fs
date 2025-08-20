module Runfs.Directives

open Runfs.Utilities
open System.Text.RegularExpressions

type Directive =
    | Sdk of name: string * version: string option
    | Package of name: string * version: string option
    | Dll of path: string
    | Source of path: string
    | Project of path: string
    | Property of name: string * value: string

let private tryParseDirective (lineNumber, line: string) =
    let tryParse separator (argument: string) lineNumber =
        let m = Regex.Match(argument, $"""^(?<part1>[^{separator}]+)({separator}\s*(?<part2>.+))?\s*$""")
        if m.Success then
            let part1 = m.Groups.["part1"].Value.Trim()
            let part2 = 
                if m.Groups.["part2"].Success then Some(m.Groups.["part2"].Value.Trim())
                else None
            let m = Regex.Match(part1, @"^(?<part1>.+)\.dll$")
            Some(part1, part2)
        else
            Error lineNumber $"Invalid directive argument {argument}"
            None

    let line = line.TrimStart()
    if line.StartsWith("#") then
        match line.IndexOf(' ') with
        | -1 -> None
        | index ->
            let kind = line.[1 .. index - 1]
            let argument = line.[index + 1 ..].Trim()
            if argument.Length = 0 then
                Error lineNumber "Directive argument is empty"
                None
            elif not (argument.StartsWith('"') && argument.EndsWith('"')) then
                Error lineNumber "Directive argument is not quoted"
                None
            else
                let argument = argument.[1 .. argument.Length - 2] // Remove quotes
                match kind with
                | "sdk" -> tryParse "," argument lineNumber |> Option.map Sdk
                | "package" -> tryParse "," argument lineNumber |> Option.map Package
                | "dll" -> Some(Dll argument)
                | "source" -> Some (Source argument)
                | "project" -> Some(Project argument)
                | "property" ->
                    tryParse "=" argument lineNumber
                    |> Option.map (fun (name, value) -> Property(name, Option.defaultValue "" value))
                | _ -> None
    else
        None

let getDirectives sourceLines =
    sourceLines |> List.mapi (fun i line -> i + 1, line) |> List.choose tryParseDirective
