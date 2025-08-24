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

let verbosity = "normal"

type Project = {projectInstance: ProjectInstance; parameters: BuildParameters}

let createProject projectFilePath projectFileText : Project =
    let loggerArgs = [|$"--verbosity:{verbosity}"|]
    let consoleLogger = TerminalLogger.CreateTerminalOrConsoleLogger loggerArgs
    let loggers = [|consoleLogger|]
    let globalProperties = Collections.Generic.Dictionary()  // TODO: empty ok?
    let projectCollection = new ProjectCollection(
        globalProperties,
        loggers,
        ToolsetDefinitionLocations.Default)
    let createProjectRootElement() =
        let reader = new StringReader(projectFileText)
        let xmlReader = XmlReader.Create reader
        let projectRoot = ProjectRootElement.Create(xmlReader, projectCollection)
        projectRoot.FullPath <- projectFilePath
        projectRoot
    let projectRoot = createProjectRootElement();
    let options = ProjectOptions()
    options.ProjectCollection <- projectCollection
    options.GlobalProperties <- globalProperties
    let projectInstance = ProjectInstance.FromProjectRootElement(projectRoot, options)
    let parameters = BuildParameters(projectCollection)
    parameters.Loggers <- loggers
    parameters.LogTaskInputs <- false
    {projectInstance = projectInstance; parameters = parameters}

let restore project parameters =
    let buildManager = BuildManager.DefaultBuildManager
    let flags =
        BuildRequestDataFlags.ClearCachesAfterBuild
        ||| BuildRequestDataFlags.SkipNonexistentTargets
        ||| BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports
        ||| BuildRequestDataFlags.FailOnUnresolvedSdk
    let restoreRequest =
        new BuildRequestData(project, [|"Restore"|], null, flags)

    buildManager.BeginBuild parameters
    let restoreResult = buildManager.BuildRequest restoreRequest
    if restoreResult.OverallResult <> BuildResultCode.Success then 1
    else 0
