module Runfs.Build

open Microsoft.Build.Construction
open Microsoft.Build.Definition
open Microsoft.Build.Evaluation
open Microsoft.Build.Execution
open Microsoft.Build.Framework
open Microsoft.Build.Logging
open Microsoft.Build.Locator
open System
open System.IO
open System.Xml
open Runfs.ProjectFile

let private verbosity = "quiet"

type Project = {projectInstance: ProjectInstance; parameters: BuildParameters}
type MsRestoreError = MsRestoreError of string

let initBuild() = MSBuildLocator.RegisterDefaults() |> ignore
let finishBuild() = BuildManager.DefaultBuildManager.EndBuild()

let createProject projectFilePath (projectFileText: string) : Project =
    let loggerArgs = [|$"--verbosity:{verbosity}"; "NoSummary"|]
    let consoleLogger = TerminalLogger.CreateTerminalOrConsoleLogger loggerArgs
    let loggers = [|consoleLogger|]
    let globalProperties =
        dict [
        ]
    let projectCollection = new ProjectCollection(
        globalProperties,
        loggers,
        ToolsetDefinitionLocations.Default)
    let options = ProjectOptions()
    options.ProjectCollection <- projectCollection
    options.GlobalProperties <- globalProperties
    // let createProjectRootElement() =
    //     let reader = new StringReader(projectFileText)
    //     let xmlReader = XmlReader.Create reader
    //     let projectRoot = ProjectRootElement.Create(xmlReader, projectCollection)
    //     projectRoot.FullPath <- projectFilePath
    //     projectRoot
    // let projectRoot = createProjectRootElement();
    // let projectInstance = ProjectInstance.FromProjectRootElement(projectRoot, options)
    File.WriteAllText(projectFilePath, projectFileText)
    let projectInstance = ProjectInstance.FromFile(projectFilePath, options)
    let parameters = BuildParameters(projectCollection)
    parameters.Loggers <- loggers
    parameters.LogTaskInputs <- false
    {projectInstance = projectInstance; parameters = parameters}

let restore (project: Project) =
    let buildManager = BuildManager.DefaultBuildManager
    let flags =
        BuildRequestDataFlags.ClearCachesAfterBuild
        ||| BuildRequestDataFlags.SkipNonexistentTargets
        ||| BuildRequestDataFlags.IgnoreMissingEmptyAndInvalidImports
        ||| BuildRequestDataFlags.FailOnUnresolvedSdk
    let restoreRequest =
        new BuildRequestData(project.projectInstance, [|"Restore"|], null, flags)

    buildManager.BeginBuild project.parameters
    let restoreResult = buildManager.BuildRequest restoreRequest
    if restoreResult.OverallResult = BuildResultCode.Success then
        Ok()
    else
        Error(MsRestoreError (string restoreResult))
