module Runfs.Directives

open FsToolkit.ErrorHandling
open System.Xml

type Directive =
    | Sdk of name: string * version: string option
    | Package of name: string * version: string option
    | Dll of path: string
    | Source of path: string
    | Project of path: string
    | Property of name: string * value: string

type ParseError =
    | UnknownKind of int * string
    | MissingArgument of int
    | ArgumentNotQuoted of int * string
    | InvalidPropertyName of int * string
    | MissingPropertyValue of int * string

let private isValidXmlName name =
    try
        XmlConvert.VerifyName name |> ignore
        true
    with ex -> false

// TODO: more detailed validity checks (like on path names)
let private argParsers = Map [
    "sdk", fun(lineNumber, argument: string) ->
        let name, version =
            match argument.IndexOf '@' with
            | -1 -> argument, None
            | index -> argument[.. index - 1].Trim(), Some (argument[index + 1 ..].Trim())
        Ok(Sdk(name, version))

    "package", fun(lineNumber, argument: string) ->
        let name, version =
            match argument.IndexOf '@' with
            | -1 -> argument, None
            | index -> argument[.. index - 1].Trim(), Some (argument[index + 1 ..].Trim())
        Ok(Package(name, version))
    
    "dll", fun(lineNumber, argument: string) -> Ok(Dll argument)
    "source", fun(lineNumber, argument: string) -> Ok(Source argument)
    "project", fun(lineNumber, argument: string) -> Ok(Project argument)

    "property", fun(lineNumber, argument: string) ->
        match argument.IndexOf '=' with
        | -1 ->
            Error(MissingPropertyValue(lineNumber, argument))
        | index ->
            let name = argument[.. index - 1].Trim()
            let value = argument[index + 1 ..].Trim()
            if isValidXmlName name then
                Ok(Property(name, value))
            else
                Error(InvalidPropertyName(lineNumber, argument))
    ]

let private tryFindRunfsDirective (lineNumber, line: string) =
    if line.StartsWith "#r_" then Some (lineNumber, line[3..]) else None

let private tryParseDirective (lineNumber, line: string) =
    match line.IndexOf(' ') with
    | -1 -> Error (MissingArgument lineNumber)
    | index ->
        let kind = line.[.. index - 1]
        let argument = line.[index + 1 ..].Trim()
        if argument.Length = 0 then
            Error (MissingArgument lineNumber)
        elif not (argument.StartsWith('"') && argument.EndsWith('"')) then
            Error (ArgumentNotQuoted(lineNumber, argument))
        else
            let argument = argument.[1 .. argument.Length - 2] // Remove quotes
            match argParsers.TryFind kind with
            | None -> Error (UnknownKind(lineNumber, kind))
            | Some parse -> parse(lineNumber, argument)

let getDirectives sourceLines =
    sourceLines
    |> List.mapi (fun i line -> i + 1, line)
    |> List.choose tryFindRunfsDirective
    |> List.map tryParseDirective
    |> List.sequenceResultA
