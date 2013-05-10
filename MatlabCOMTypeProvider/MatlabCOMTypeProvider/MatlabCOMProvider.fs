namespace FSMatlab.COMTypeProvider

open System
open System.Reflection
open System.Diagnostics
open System.Threading
open System.Collections.Generic
open ProviderImplementation.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations

open FSMatlab.COMInterface

module MatlabInterface =    
    let executor = MatlabCommandExecutor(new MatlabCOMProxy("Matlab.Desktop.Application"))


[<TypeProvider>]
type MatlabCOMProvider (config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    let rootNamespace = "Matlab"
    let thisAssembly  = Assembly.GetExecutingAssembly()

    let mlKind = "Matlab.Desktop.Application"
    let proxy = MatlabCOMProxy mlKind  
    let executor = MatlabCommandExecutor proxy

    let pty = ProvidedTypeDefinition(thisAssembly,rootNamespace,"Vars", Some(typeof<obj>))
    do pty.AddMembersDelayed(fun () ->                 
            [
                for var in executor.GetVariableInfos() do
                    let mltype = executor.GetVariableMatlabType(var)
                    let p = ProvidedProperty(
                                propertyName = var.Name, 
                                propertyType = executor.GetVariableDotNetType(var), 
                                IsStatic = true,
                                GetterCode = fun args -> let name = var.Name in <@@ MatlabInterface.executor.GetVariableContents name mltype @@>)
                    p.AddXmlDocDelayed(fun () -> sprintf "%A" var)
                    yield p                   
            ]
        )
    do this.AddNamespace(rootNamespace, [ pty ])

    let packages = executor.GetPackageNames()
    let pkgNs = rootNamespace + ".Packages"
    do for package in packages do                 
           let pkgTyp = ProvidedTypeDefinition(thisAssembly, pkgNs, package, Some typeof<obj>)
           pkgTyp.AddXmlDocDelayed(fun () -> executor.GetPackageHelp(package))

           pkgTyp.AddMembersDelayed(fun () -> 
                [
//                    let pkgFuncs = executor.GetPackageFunctions(package)
//                    for pkgFunc in pkgFuncs do
//                        ProvidedMethod methodName = memberName,
//                                         parameters = paramList,
//                                         returnType = typeof<SymbolicExpression>,
//                                         IsStaticMethod = true,
                ]
           )
           do this.AddNamespace(pkgNs, [pkgTyp])


[<assembly:TypeProviderAssembly>] 
do()