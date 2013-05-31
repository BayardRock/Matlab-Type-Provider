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

module LazyProviderHelpers =

    let applyArgsToHandle (handle: IMatlabFunctionHandle, args: obj []) =
        handle.Apply(args)
    
    let generateFunctionHandlesFromDescription (executor: MatlabCommandExecutor) (tb: MatlabToolboxInfo) (mlfun: MatlabFunctionInfo) =
        let funcParam =
            let pp = ProvidedParameter("args", typeof<obj[]>, optionalValue=null)
            pp.IsParamArray <- true; pp

        let outputType = typeof<obj []>
        let getXmlText () = executor.GetFunctionHelp tb mlfun       

        let pm = ProvidedMethod(
                        methodName = mlfun.Name,
                        parameters = [funcParam],
                        returnType = typeof<IMatlabAppliedFunctionHandle>,
                        IsStaticMethod = true,
                        InvokeCode = fun args -> 
                                        let functionPath = mlfun.Path
                                        let name = mlfun.Name 
                                        let numout = mlfun.OutParams.Length    
                                        let arrArgs = args.[0]
                                        <@@ 
                                            //failwith (sprintf "Varargs: %A" (%%varInArgs : obj []))
                                            let finfo =  MatlabInterface.executor.GetFunctionInfoFromFile functionPath
                                            let fhandle = MatlabInterface.executor.ConvertFunctionInfoToFunctionHandle(finfo)
                                            applyArgsToHandle(fhandle, (%%arrArgs: obj[]))
                                            //applyFunction (name, numout, (%%namedInArgs : obj[]), (%%varInArgs : obj[]), hasVarargout) 
                                        @@>)
        pm.AddXmlDocDelayed(fun () -> getXmlText ())
        pm

[<TypeProvider>]
type LazyMatlabProvider (config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    let rootNamespace = "LazyMatlab"
    let thisAssembly  = Assembly.GetExecutingAssembly()

    let mlKind = "Matlab.Desktop.Application"
    let proxy = FSMatlab.MatlabCOM.MatlabCOMProxy mlKind  
    let executor = MatlabCommandExecutor proxy

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
                            yield LazyProviderHelpers.generateFunctionHandlesFromDescription executor tb func :> MemberInfo
                    ])
                tbType.AddXmlDocDelayed(fun () -> getTBXML ())
                yield tbType
        ])
    do fty.AddXmlDoc("Matlab toolboxes and functions")
    do this.AddNamespace(rootNamespace,  [fty])
