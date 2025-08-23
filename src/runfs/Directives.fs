module Runfs.Directives

open Runfs.Utilities
open FsToolkit.ErrorHandling
open System.Text.RegularExpressions

type Directive =
    | Sdk of name: string * version: string option
    | Package of name: string * version: string option
    | Dll of path: string
    | Source of path: string
    | Project of path: string
    | Property of name: string * value: string

type ParseError =
    | InvalidDirectiveArgument of int * string

let tryParseTwoPartArgument regex lineNumber argument =
    let m = Regex.Match(argument, regex)
    if m.Success then
        let part1 = m.Groups.["part1"].Value.Trim()
        let part2 = 
            if m.Groups.["part2"].Success then Some(m.Groups.["part2"].Value.Trim())
            else None
        Ok(part1, part2)
    else
        Error (InvalidDirectiveArgument(lineNumber, argument))

let tryParseArgument regex lineNumber argument =
    let m = Regex.Match(argument, regex)
    if m.Success then
        let part1 = m.Groups.["part1"].Value.Trim()
        let part2 = 
            if m.Groups.["part2"].Success then Some(m.Groups.["part2"].Value.Trim())
            else None
        Ok(part1, part2)
    else
        Error (InvalidDirectiveArgument(lineNumber, argument))

let argParsers = [
    "r_sdk", tryParseTwoPartArgument """^([^,]+)(,\s*(.+))?\s*$"""
    "r_package", tryParseTwoPartArgument """^([^,]+)(,\s*(.+))?\s*$"""
    "r_dll", tryParseArgument """^([^<>:"?\|\*\\]+\.dll)"""
    "r_source", tryParseArgument """^([^<>:"?\|\*\\]+\.fs)"""
    "r_project", tryParseArgument """^([^<>:"?\|\*\\]+\...(?:fs|cs))"""
    "r_property", tryParseArgument ""
    ]

let private isDirectiveLine (line: string) =
    line.StartsWith '#'


let private tryParseDirective (lineNumber, line: string) =
    let tryParseArgument separator (argument: string) lineNumber =
        let m = Regex.Match(argument, $"""^(?<part1>[^{separator}]+)({separator}\s*(?<part2>.+))?\s*$""")
        if m.Success then
            let part1 = m.Groups.["part1"].Value.Trim()
            let part2 = 
                if m.Groups.["part2"].Success then Some(m.Groups.["part2"].Value.Trim())
                else None
            Ok(part1, part2)
        else
            Error (InvalidDirectiveArgument(lineNumber, argument))

    let line = line.TrimStart()
    if line.StartsWith("#") then
        match line.IndexOf(' ') with
        | -1 -> None
        | index ->
            let kind = line.[1 .. index - 1]
            let argument = line.[index + 1 ..].Trim()
            if argument.Length = 0 then
                ParseError lineNumber "Directive argument is empty"
                None
            elif not (argument.StartsWith('"') && argument.EndsWith('"')) then
                ParseError lineNumber "Directive argument is not quoted"
                None
            else
                let argument = argument.[1 .. argument.Length - 2] // Remove quotes
                match kind with
                | "r_sdk" -> tryParseArgument "," argument lineNumber |> Result.map Sdk |> Some
                | "r_package" -> tryParseArgument "," argument lineNumber |> Result.map Package |> Some
                | "r_dll" -> Dll argument |> Ok |> Some
                | "r_source" -> Source argument |> Ok |> Some
                | "r_project" -> Project argument |> Ok |> Some
                | "r_property" ->
                    tryParseArgument "=" argument lineNumber
                    |> Result.map (fun (name, value) -> Property(name, Option.defaultValue "" value))
                    |> Some
                | _ -> None
    else
        None

let getDirectives sourceLines =
    sourceLines
    |> List.mapi (fun i line -> i + 1, line)
    |> List.choose tryParseDirective
    |> List.sequenceResultA
