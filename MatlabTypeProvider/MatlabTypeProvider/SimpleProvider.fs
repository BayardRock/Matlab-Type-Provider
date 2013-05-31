namespace FSMatlab

open System
open System.Reflection
open System.Diagnostics
open System.Threading
open System.Collections.Generic
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations

open Samples.FSharp.ProvidedTypes
open FSMatlab.InterfaceTypes
open FSMatlab.Interface

module MatlabInterface =    
    let executor = lazy (MatlabCommandExecutor(new FSMatlab.MatlabCOM.MatlabCOMProxy("Matlab.Desktop.Application")))

module SimpleProviderHelpers = 
    let generateTypesForMatlabVariables (executor: MatlabCommandExecutor) = 
            [
                for var in executor.GetVariableInfos() do
                    let mltype = TypeConverters.getMatlabTypeFromMatlabSig(var)
                    let p = ProvidedProperty(
                                propertyName = var.Name, 
                                propertyType = TypeConverters.getDotNetType(mltype), 
                                IsStatic = true,
                                GetterCode = fun args -> let name = var.Name in <@@ MatlabInterface.executor.Force().GetVariableContents(name, mltype) @@>)
                    p.AddXmlDocDelayed(fun () -> sprintf "%A" var)
                    yield p                   
            ]

    let internal getParamsForFunctionInputs (mlfun: MatlabFunctionInfo) =
        let hasVarargin = match mlfun.InParams |> List.rev with | "varargin" :: rest -> true | _ -> false
        [                     
            for p in mlfun.InParams do
                if p <> "varargin" then
                    yield ProvidedParameter(p, typeof<obj>, optionalValue=null) 
                else yield ProvidedParameter(p, typeof<obj []>, optionalValue=null)
        ], hasVarargin

    let internal getParamsForFunctionOutputs (mlfun: MatlabFunctionInfo) = 
        let hasVarargout = match mlfun.OutParams |> List.rev with | "varargout" :: rest -> true | _ -> false
        let typ = match mlfun.OutParams.Length with
                  | 1 when hasVarargout -> typeof<obj array>
                  | 1 -> typeof<obj>
                  | n -> 
                      let outparams =  
                          seq {
                              for i = 0 to mlfun.OutParams.Length - 2 do
                                  yield typeof<obj>
                              yield if hasVarargout then typeof<obj array> else typeof<obj>
                          } |> Seq.toArray
                      Microsoft.FSharp.Reflection.FSharpType.MakeTupleType(outparams)
        typ, hasVarargout       

    /// Generates a Method in which all of the parameters are optional, and the output is a tuple with the entire result set, even optionals
    let generateFullFunctionCallsFromDescription (executor: MatlabCommandExecutor) (tb: MatlabToolboxInfo) (mlfun: MatlabFunctionInfo) =
        let funcParams, hasVarargin = getParamsForFunctionInputs mlfun
        let outputType, hasVarargout = getParamsForFunctionOutputs mlfun
        let getXmlText () = executor.GetFunctionHelp tb mlfun        
       
        let pm = ProvidedMethod(
                        methodName = mlfun.Name,
                        parameters = funcParams,
                        returnType = outputType,
                        IsStaticMethod = true,
                        InvokeCode = fun args -> 
                                        let name = mlfun.Name 
                                        let numout = mlfun.OutParams.Length    
                                        let arrArgs = args |> List.toArray
                                        let castValues = 
                                            let stopIdx = if hasVarargin then arrArgs.Length - 2 else arrArgs.Length - 1
                                            [ for expr in arrArgs.[0 .. stopIdx] do yield Quotations.Expr.Coerce(expr, typeof<obj>) ] 

                                        let varInArgs = 
                                            if hasVarargin then arrArgs.[arrArgs.Length - 1] else Quotations.Expr.NewArray(typeof<obj>, [])

                                        let namedInArgs = Quotations.Expr.NewArray(typeof<obj>, castValues)
                                        <@@ 
                                            //failwith (sprintf "Varargs: %A" (%%varInArgs : obj []))
                                            MatlabInterface.executor.Force().CallFunctionWithValues(name, numout, (%%namedInArgs : obj[]), (%%varInArgs : obj[]), hasVarargout) 
                                        @@>)
        pm.AddXmlDocDelayed(fun () -> getXmlText ())
        pm
        

[<TypeProvider>]
type SimpleMatlabProvider (config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    let rootNamespace = "SimpleMatlab"
    let thisAssembly  = Assembly.GetExecutingAssembly()

    let mlKind = "Matlab.Desktop.Application"
    let proxy = FSMatlab.MatlabCOM.MatlabCOMProxy mlKind  
    let executor = MatlabCommandExecutor proxy
    
    //
    // Base Variables
    //
    let pty = ProvidedTypeDefinition(thisAssembly,rootNamespace,"Vars", Some(typeof<obj>))
    do pty.AddMembersDelayed(fun () -> SimpleProviderHelpers.generateTypesForMatlabVariables executor)
    do pty.AddXmlDoc("This contains all variables which are bound when the type provider is loaded")
    do this.AddNamespace(rootNamespace,  [pty])

    //
    // Toolboxes and Functions 
    //
    let fty = ProvidedTypeDefinition(thisAssembly, rootNamespace, "Toolboxes", Some(typeof<obj>))
    do fty.AddMembersDelayed(fun () -> 
        [
            let toolboxes = executor.GetToolboxes()
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
                            yield SimpleProviderHelpers.generateFullFunctionCallsFromDescription executor tb func :> MemberInfo
                    ])
                tbType.AddXmlDocDelayed(fun () -> getTBXML ())
                yield tbType
        ])
    do fty.AddXmlDoc("Matlab toolboxes and functions")
    do this.AddNamespace(rootNamespace,  [fty])


    //
    // Packages   
    // Note: Currently Very Experimental, and not very useful
    //
//    let packages = executor.GetPackageNames()
//    let pkgNs = rootNamespace + ".Packages"
//    do for package in packages do                 
//           let pkgTyp = ProvidedTypeDefinition(thisAssembly, pkgNs, package, Some typeof<obj>)
//           pkgTyp.AddXmlDocDelayed(fun () -> executor.GetPackageHelp(package))
//
//           pkgTyp.AddMembersDelayed(fun () -> 
//                [
//                    let pkgFuncs = executor.GetPackageFunctions(package)
//                    for pkgFunc in pkgFuncs do
//                        let funcParams = [ for p in pkgFunc.InArgs -> ProvidedParameter(pkgFunc.Name, typeof<obj>, optionalValue=null) ]
//                        let pm = ProvidedMethod(
//                                    methodName = pkgFunc.Name,
//                                    parameters = funcParams,
//                                    returnType = typeof<obj>,
//                                    IsStaticMethod = true,
//                                    InvokeCode = fun args -> 
//                                                    let name = pkgFunc.Name 
//                                                    let namedArgs = Quotations.Expr.NewArray(typeof<obj>, args)
//                                                    <@@ MatlabInterface.executor.CallFunction name %%namedArgs @@>)
//                        pm.AddXmlDocDelayed(fun () -> executor.GetMethodHelp package pkgFunc.Name)
//                        yield pm
//                ]
//           )         
//           do this.AddNamespace(pkgNs,  [pkgTyp])



[<assembly:TypeProviderAssembly>] 
do()