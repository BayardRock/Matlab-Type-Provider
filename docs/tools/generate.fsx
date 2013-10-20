﻿// --------------------------------------------------------------------------------------
// Builds the documentation from `.fsx` and `.md` files in the 'docs/content' directory
// (the generated documentation is stored in the 'docs/output' directory)
// --------------------------------------------------------------------------------------


// Binaries that have XML documentation (in a corresponding generated XML file)
let referenceBinaries = [ "MatlabTypeProvider.dll" ]
// Web site location for the generated documentation
let website = "http://bayardrock.github.io/Matlab-Type-Provider"

// Specify more information about your project
let info =
  [ "project-name", "Matlab Type Provider"
    "project-author", "Rick Minerich"
    "project-summary", "A powerful yet easy to use library for F# to Matlab interoperability"
    "project-github", "https://github.com/BayardRock/Matlab-Type-Provider"
    "project-nuget", "http://nuget.com/packages/MatlabTypeProvider" ]

// --------------------------------------------------------------------------------------
// For typical project, no changes are needed below
// --------------------------------------------------------------------------------------

#I "../../packages/FSharp.Formatting.2.0.4/lib/net40"
#r "../../packages/RazorEngine.3.3.0/lib/net40/RazorEngine.dll"
#r "../../packages/Microsoft.AspNet.Razor.2.0.30506.0/lib/net40/System.Web.Razor.dll"
#r "../../packages/FAKE/tools/FakeLib.dll"
#r "FSharp.Literate.dll"
#r "FSharp.CodeFormat.dll"
#r "FSharp.MetadataFormat.dll"
open Fake
open System.IO
open Fake.FileHelper
open FSharp.Literate
open FSharp.MetadataFormat
let (++) a b = Path.Combine(a, b)

// When called from 'build.fsx', use the public project URL as <root>
// otherwise, use the current 'output' directory.
#if RELEASE
let root = website
#else
let root = "file://" + (__SOURCE_DIRECTORY__ ++ "../output")
#endif

// Paths with template/source/output locations
let bin      = __SOURCE_DIRECTORY__ ++ "../../bin"
let content  = __SOURCE_DIRECTORY__ ++ "../content"
let output   = __SOURCE_DIRECTORY__ ++ "../output"
let files    = __SOURCE_DIRECTORY__ ++ "../files"
let template = __SOURCE_DIRECTORY__ ++ "template.html"
let literate = __SOURCE_DIRECTORY__ ++ "../../packages/FSharp.Formatting.2.0.4/literate/content"
let referenceTemplate = __SOURCE_DIRECTORY__ ++ "reference"

// Build API reference from XML comments
let buildReference () = 
  CleanDir (output ++ "reference")
  for lib in referenceBinaries do
    MetadataFormat.Generate(bin ++ lib, output ++ "reference", referenceTemplate)

// Build documentation from `fsx` and `md` files in `docs/content`
let buildDocumentation () =
  CopyRecursive files output true |> Log "Copying file: "
  ensureDirectory (output ++ "styles")
  CopyRecursive literate (output ++ "styles") true |> Log "Copying styles: "
  let subdirs = Directory.EnumerateDirectories(content, "*", SearchOption.AllDirectories)
  for dir in Seq.append [content] subdirs do
    let sub = if dir.Length > content.Length then dir.Substring(content.Length + 1) else "."
    Literate.ProcessDirectory
      ( dir, template, output ++ sub, 
        replacements = ("root", root)::info )

// Generate 
buildDocumentation()
buildReference()