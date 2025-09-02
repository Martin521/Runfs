module Runfs.Build

open Microsoft.Build.Construction
open Microsoft.Build.Definition
open Microsoft.Build.Evaluation
open Microsoft.Build.Execution
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open System
open System.IO
open System.Xml
open Runfs.ProjectFile

type Project =
    {buildManager: BuildManager; projectInstance: ProjectInstance}
    interface IDisposable with
        member this.Dispose() =
            this.buildManager.EndBuild()
            this.projectInstance.FullPath |> File.Delete

type MSBuildError = MSBuildError of target: string * result: string

let initMSBuild() = Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults() |> ignore

let createProject verbose projectFilePath (projectFileText: string) : Project =
    let verbosity = if verbose then "m" else "q"
    let loggerArgs = [|$"-verbosity:{verbosity}"; "-tl:off"; "NoSummary"|]
    let consoleLogger = TerminalLogger.CreateTerminalOrConsoleLogger loggerArgs
    let loggers = [|consoleLogger|]
    let globalProperties = dict []
    let projectCollection = new ProjectCollection(
        globalProperties,
        loggers,
        ToolsetDefinitionLocations.Default)
    let options = ProjectOptions()
    options.ProjectCollection <- projectCollection
    options.GlobalProperties <- globalProperties

    let reader = new StringReader(projectFileText)
    let xmlReader = XmlReader.Create reader
    let projectRoot = ProjectRootElement.Create(xmlReader, projectCollection)
    projectRoot.FullPath <- projectFilePath
    let projectInstance = ProjectInstance.FromProjectRootElement(projectRoot, options)
    let parameters = BuildParameters projectCollection
    parameters.Loggers <- loggers
    parameters.LogTaskInputs <- false
    let buildManager = BuildManager.DefaultBuildManager
    buildManager.BeginBuild parameters
    {buildManager = buildManager; projectInstance = projectInstance}

let build target project =
    let flags =
        BuildRequestDataFlags.ClearCachesAfterBuild
        ||| BuildRequestDataFlags.SkipNonexistentTargets
        ||| BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports
        ||| BuildRequestDataFlags.FailOnUnresolvedSdk
    let buildRequest =
        new BuildRequestData(project.projectInstance, [|target|], null, flags)

    let buildResult = project.buildManager.BuildRequest buildRequest
    if buildResult.OverallResult = BuildResultCode.Success then
        Ok()
    else
        Error(MSBuildError(target, string buildResult))
