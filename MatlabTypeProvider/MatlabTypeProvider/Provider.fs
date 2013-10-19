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

[<CompilerMessageAttribute("For Type Provider internal use only.", 10001)>] // Special number blessed by Tomas
module MatlabInterface = 
    let private comProxy = new FSMatlab.MatlabCOM.MatlabCOMProxy("Matlab.Desktop.Application")
    let executor = MatlabCommandExecutor(comProxy)
    let toolboxeCache = lazy (executor.GetToolboxes() |> Seq.toList |> InterfaceHelpers.MatlabFunctionHelpers.nestAllToolboxes)

module ProviderHelpers = 
        let internal getParamsForFunctionInputs (mlfun: MatlabFunctionInfo) =
            let hasVarargin = match mlfun.InParams |> List.rev with | "varargin" :: rest -> true | _ -> false
            [                     
                for p in mlfun.InParams do
                    if p <> "varargin" then yield ProvidedParameter(p, typeof<obj>, optionalValue = null) 
                    else                    yield ProvidedParameter(p, typeof<obj []>, IsParamArray = true)
            ], hasVarargin

module LazyProviderHelpers =

    let generateTypesForMatlabVariableHandles (executor: MatlabCommandExecutor) = 
            [
                for info in executor.GetVariableInfos() do
                    let handle = executor.GetVariableHandle(info)
                    let p = ProvidedProperty(
                                propertyName = info.Name, 
                                propertyType = typeof<MatlabVariableHandle>, 
                                IsStatic = true,
                                GetterCode = fun args -> 
                                    let name = info.Name in 
                                        <@@ 
                                            MatlabInterface.executor.GetVariableHandle(name) 
                                        @@>)
                    p.AddXmlDocDelayed(fun () -> sprintf "%A" info)
                    yield p                   
            ]

    let generateFunctionHandlesFromDescription (executor: MatlabCommandExecutor) (tb: MatlabToolboxInfo) (mlfun: MatlabFunctionInfo) =
        let funcParams, hasVarargin = ProviderHelpers.getParamsForFunctionInputs mlfun

        let outputType = typeof<obj []>
        let getXmlText () = executor.GetFunctionHelp tb mlfun       

        let pm = ProvidedMethod(
                        methodName = mlfun.Name,
                        parameters = funcParams,
                        returnType = typeof<MatlabAppliedFunctionHandle>,
                        IsStaticMethod = true,
                        InvokeCode = fun args -> 
                                        let functionPath = mlfun.Path
                                        let name = mlfun.Name 
                                        let numout = mlfun.OutParams.Length
                                        let arrArgs = args |> List.toArray

                                        let varInArgs = 
                                            if hasVarargin then arrArgs.[arrArgs.Length - 1]
                                            else Quotations.Expr.NewArray(typeof<obj>, [])

                                        let namedInArgs = if hasVarargin then
                                                               let nArgs = arrArgs.[0 .. arrArgs.Length - 2] 
                                                               Quotations.Expr.NewArray(typeof<obj>, nArgs |> Array.toList)
                                                          else Quotations.Expr.NewArray(typeof<obj>, args)

                                        <@@ 
                                            let finfo = MatlabInterface.executor.GetFunctionInfoFromName name
                                            let fhandle = MatlabInterface.executor.GetFunctionHandle(finfo)
                                            let vettedInArgs = 
                                                let namedInArgs = (%%namedInArgs : obj[])
                                                match namedInArgs |> Array.tryFindIndex (fun e -> e.GetType() = typeof<System.Reflection.Missing> )with
                                                | Some nullIdx -> namedInArgs.[0 .. nullIdx - 1]
                                                | None -> Array.append namedInArgs (%%varInArgs: obj[])
                                            fhandle.Apply(vettedInArgs)
                                        @@>)
        pm.AddXmlDocDelayed(fun () -> getXmlText ())
        pm

    let rec generateToolboxes (executor: MatlabCommandExecutor) (thisAssembly: Assembly) (parentNamespace: string) (toolboxes: MatlabToolboxInfo list) : (string * ProvidedTypeDefinition) list = 
        [
            for tb in toolboxes do
                let tbNamespace = parentNamespace + "." + tb.Name
                let tbType = ProvidedTypeDefinition(thisAssembly, tbNamespace, "M", Some(typeof<obj>))
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

                        // Generate Toolbox Functions
                        yield! tb.Funcs |> Seq.map (fun func -> generateFunctionHandlesFromDescription executor tb (func.Force()) :> MemberInfo)
                    ])
                tbType.AddXmlDocDelayed(fun () -> getTBXML ())
                yield tbNamespace, tbType
                // Generate Sub-toolboxes                        
                yield! generateToolboxes executor thisAssembly tbNamespace (tb.Toolboxes) //|> Seq.cast<MemberInfo>
          ]

[<TypeProvider>]
type SimpleMatlabProvider (config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    let strictRootNamespace = "FSMatlab"
    let lazyRootNamespace = "FSMatlab"
    let thisAssembly  = Assembly.GetExecutingAssembly()

    let mlKind = "Matlab.Desktop.Application"
    let proxy = new FSMatlab.MatlabCOM.MatlabCOMProxy(mlKind)  
    let executor = new MatlabCommandExecutor(proxy)
    
    //
    // Handles to Existing Variables
    //
    let pty = ProvidedTypeDefinition(thisAssembly,strictRootNamespace, "Vars", Some(typeof<obj>))
    do pty.AddMembersDelayed(fun () -> LazyProviderHelpers.generateTypesForMatlabVariableHandles executor)
    do pty.AddXmlDoc("This contains all variables which are bound when the type provider is loaded")
    do this.AddNamespace(strictRootNamespace,  [pty])

    //
    // Lazy Toolboxes and Functions 
    //
    let toolboxNamespace = "Toolboxes"
//    let fty = ProvidedTypeDefinition(thisAssembly, lazyRootNamespace, "Toolboxes", Some(typeof<obj>))
    let toolboxInfos = MatlabInterface.toolboxeCache.Value
    let toolboxObjects = LazyProviderHelpers.generateToolboxes executor thisAssembly toolboxNamespace toolboxInfos
    do toolboxObjects |> List.iter (fun (ns, tb) -> this.AddNamespace(ns, [tb])) 
    //do fty.AddXmlDoc("Matlab toolboxes with function handles")
    //do this.AddNamespace(lazyRootNamespace,  [fty])


[<assembly:TypeProviderAssembly>] 
do()