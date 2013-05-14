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
    let executor = MatlabCommandExecutor(new MatlabCOM.MatlabCOMProxy("Matlab.Desktop.Application"))


[<TypeProvider>]
type MatlabCOMProvider (config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    let rootNamespace = "Matlab"
    let thisAssembly  = Assembly.GetExecutingAssembly()

    let mlKind = "Matlab.Desktop.Application"
    let proxy = MatlabCOM.MatlabCOMProxy mlKind  
    let executor = MatlabCommandExecutor proxy
    
    //
    // Base Variables
    //
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
            ])
    do pty.AddXmlDoc("This contains all variables which are bound when the type provider is loaded")
    do this.AddNamespace(rootNamespace,  [pty])

    //
    // Toolboxes and Functions 
    //
    let fty = ProvidedTypeDefinition(thisAssembly, rootNamespace, "Toolboxes", Some(typeof<obj>))
    do fty.AddMembersDelayed(fun () -> 
        [
            let searchPaths = executor.GetFunctionSearchPaths()
            let matlabRoot = executor.GetRoot()
            let toolboxes = MatlabFunctionHelpers.toolboxesFromPaths matlabRoot searchPaths
            for tb in toolboxes do
                let tbType = ProvidedTypeDefinition(tb.Name, Some(typeof<obj>))
                do tbType.AddMembersDelayed(fun () ->
                    [
                        for func in tb.Funcs do
                            let funcParams = [ for p in func.InParams -> ProvidedParameter(func.Name, typeof<obj>, optionalValue=null) ]
                            let pm = ProvidedMethod(
                                            methodName = func.Name,
                                            parameters = funcParams,
                                            returnType = typeof<obj>,
                                            IsStaticMethod = true,
                                            InvokeCode = fun args -> 
                                                            let name = func.Name 
                                                            let namedArgs = Quotations.Expr.NewArray(typeof<obj>, args)
                                                            <@@ MatlabInterface.executor.CallFunction name %%namedArgs @@>)
                            do pm.AddXmlDocDelayed(fun () -> "Function help goes here")
                            yield pm
                    ])
                tbType.AddXmlDocDelayed(fun () -> "Toolbox help goes here")
                yield tbType
        ])
    do fty.AddXmlDoc("Matlab toolboxes and functions live here")
    do this.AddNamespace(rootNamespace,  [fty])


    //
    // Packages   
    //
    let packages = executor.GetPackageNames()
    let pkgNs = rootNamespace + ".Packages"
    do for package in packages do                 
           let pkgTyp = ProvidedTypeDefinition(thisAssembly, pkgNs, package, Some typeof<obj>)
           pkgTyp.AddXmlDocDelayed(fun () -> executor.GetPackageHelp(package))

           pkgTyp.AddMembersDelayed(fun () -> 
                [
                    let pkgFuncs = executor.GetPackageFunctions(package)
                    for pkgFunc in pkgFuncs do
                        let funcParams = [ for p in pkgFunc.InArgs -> ProvidedParameter(pkgFunc.Name, typeof<obj>, optionalValue=null) ]
                        let pm = ProvidedMethod(
                                    methodName = pkgFunc.Name,
                                    parameters = funcParams,
                                    returnType = typeof<obj>,
                                    IsStaticMethod = true,
                                    InvokeCode = fun args -> 
                                                    let name = pkgFunc.Name 
                                                    let namedArgs = Quotations.Expr.NewArray(typeof<obj>, args)
                                                    <@@ MatlabInterface.executor.CallFunction name %%namedArgs @@>)
                        pm.AddXmlDocDelayed(fun () -> executor.GetFunctionHelp package pkgFunc.Name)
                        yield pm
                ]
           )         
           do this.AddNamespace(pkgNs,  [pkgTyp])



[<assembly:TypeProviderAssembly>] 
do()