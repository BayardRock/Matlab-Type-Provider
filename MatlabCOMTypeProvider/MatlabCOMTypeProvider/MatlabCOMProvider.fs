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
                let getTBXML () = executor.GetToolboxHelp tb                
                do tbType.AddMembersDelayed(fun () ->
                    [
                        #if DEBUG
                        // Debug Property For Looking at the toolbox XMLDOC in Plain Text    
                        yield ProvidedProperty(
                            propertyName = "XMLDoc", 
                            propertyType = typeof<string>, 
                            IsStatic = true, 
                            GetterCode = let text = getTBXML () in fun args -> <@@ text @@> ) :> MemberInfo
                        #endif
                            
                        for func in tb.Funcs do
                            let funcParams = [ for p in func.InParams -> ProvidedParameter(p, typeof<obj>, optionalValue=null) ]
                            let getXmlText () = executor.GetFunctionHelp tb func
                            let outputType = 
                                    match func.OutParams.Length with
                                    | 0 -> typeof<unit>
                                    | 1 -> typeof<obj>
                                    | 2 -> typeof<Tuple<obj,obj>>
                                    | 3 -> typeof<Tuple<obj,obj,obj>>
                                    | 4 -> typeof<Tuple<obj,obj,obj,obj>>
                                    | 5 -> typeof<Tuple<obj,obj,obj,obj,obj>>
                                    | 6 -> typeof<Tuple<obj,obj,obj,obj,obj,obj>>
                                    | 7 -> typeof<Tuple<obj,obj,obj,obj,obj,obj,obj>>
                                    | 8 -> typeof<Tuple<obj,obj,obj,obj,obj,obj,obj,obj>>
                                    | _ -> typeof<obj [] ref>
                                
                            let pm = ProvidedMethod(
                                            methodName = func.Name,
                                            parameters = funcParams,
                                            returnType = outputType,
                                            IsStaticMethod = true,
                                            InvokeCode = fun args -> 
                                                            let name = func.Name 
                                                            let numout = func.OutParams.Length                                                            
                                                            let namedInArgs = Quotations.Expr.NewArray(typeof<obj>, args)
                                                            <@@ MatlabInterface.executor.CallFunction name numout %%namedInArgs @@>)

                            do pm.AddXmlDocDelayed(fun () -> getXmlText ())
                            yield pm :> MemberInfo
                    ])
                tbType.AddXmlDocDelayed(fun () -> getTBXML ())
                yield tbType
        ])
    do fty.AddXmlDoc("Matlab toolboxes and functions")
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
                        pm.AddXmlDocDelayed(fun () -> executor.GetMethodHelp package pkgFunc.Name)
                        yield pm
                ]
           )         
           do this.AddNamespace(pkgNs,  [pkgTyp])



[<assembly:TypeProviderAssembly>] 
do()