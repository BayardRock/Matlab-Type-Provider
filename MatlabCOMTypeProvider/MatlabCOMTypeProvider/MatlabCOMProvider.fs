namespace FSMatlab.COMTypeProvider

open System
open System.Reflection
open System.Diagnostics
open System.Threading
open System.Collections.Generic
open Samples.FSharp.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations

open FSMatlab.COMInterface

[<TypeProvider>]
type MatlabCOMProvider (config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    let rootNamespace = "Matlab.COMTypeProvider"
    let thisAssembly  = Assembly.GetExecutingAssembly()

    let mlKind = "Matlab.Desktop.Application"
    let proxy = lazy MatlabCOMProxy mlKind  

    let pty = ProvidedTypeDefinition(thisAssembly,rootNamespace,"MatlabVars", Some(typeof<obj>))
    do pty.AddMembersDelayed(fun () ->
      
            let executor = lazy MatlabCommandExecutor (proxy.Force())
            [
                for var in executor.Force().GetVariables() do
                    let p = ProvidedProperty(
                                propertyName = var.Name, 
                                propertyType = typeof<string>, 
                                IsStatic = true,
                                GetterCode = fun _ -> let name = var.Name in <@@ name @@>)
                    p.AddXmlDocDelayed(fun () -> sprintf "%A" var)
                    yield p                   
            ]
        )
    do this.AddNamespace(rootNamespace, [ pty ])


[<assembly:TypeProviderAssembly>] 
do()