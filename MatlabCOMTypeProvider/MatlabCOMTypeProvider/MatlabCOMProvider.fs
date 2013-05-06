namespace MatlabCOMTypeProvider

open System
open System.Reflection
open Samples.FSharp.ProvidedTypes
open Microsoft.FSharp.Core.CompilerServices
open Microsoft.FSharp.Quotations

type MatlabVariable = {
        Name: string
        Size: int list
        Bytes: uint64
        Class: string
        Attributes: string list
    }

module MatlabHelpers = 
    let parseWhos (whosstr: string) =
        let crlfchars = [|'\r'; '\n'|]
        let byline = whosstr.Split(crlfchars, StringSplitOptions.RemoveEmptyEntries) 
        let header = byline.[0].Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
        assert(header.[0] = "Name" && header.[1] = "Size" && header.[2] = "Bytes" && header.[3] = "Class" && header.[4] = "Attributes")
        [|
            for i = 1 to byline.Length - 1 do
                let bytoken = byline.[i].Split([|' '|], StringSplitOptions.RemoveEmptyEntries) 
                yield {
                    Name = bytoken.[0]
                    Size = bytoken.[1].Split([|'x'|], StringSplitOptions.RemoveEmptyEntries) |> Array.map (fun n -> int n) |> Array.toList
                    Bytes = bytoken.[2] |> uint64
                    Class = bytoken.[3]
                    Attributes = if bytoken.Length >= 5 then bytoken.[4 ..] |> Array.toList else []
                }
        |]

type MatlabCOMProxy (progid: string) =
    let mtyp = Type.GetTypeFromProgID( progid ) //"Matlab.Desktop.Application"
    let ml = Activator.CreateInstance(mtyp)

    //
    // The server returns output from the command in the string, result. The result string also contains any warning or error messages that might have been issued by MATLAB software as a result of the command.
    //
    member t.Execute args = mtyp.InvokeMember("Execute", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, args)

    //
    // Feval('functionname',numout,arg1,arg2,...) 
    //
    member t.Feval args = mtyp.InvokeMember("Feval", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, args)
    
    //
    // Read a char array from matlab as a string
    //
    member t.GetCharArray (var: string) = mtyp.InvokeMember("GetCharArray", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"|] ) :?> string

    //
    // Read a variable from matlab
    // If your scripting language requires a result be returned explicitly, use the GetVariable function in place of GetWorkspaceData, GetFullMatrix or GetCharArray.
    // Do not use GetVariable on sparse arrays, structures, or function handles.
    //
    member t.GetVariable (var: string) = mtyp.InvokeMember("GetVariable", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"|] ) 
    
    //Needs to know the size beforehand
    //member t.GetFullMatrix (var: string) = mtyp.InvokeMember("GetFullMatrix", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; xreal; ximag|] ) 
    
    ///
    /// Use GetWorkspaceData instead of GetFullMatrix and GetCharArray to get numeric and character array data, respectively. Do not use GetWorkspaceData on sparse arrays, structures, or function handles.
    /// These functions use the variant data type instead of the safearray data type used by GetFullMatrix and PutFullMatrix.
    ///
    member t.GetWorkspaceData (var: string) = mtyp.InvokeMember("GetWorkspaceData", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"|] ) 

    member t.PutCharArray (var:string) (value:string) =  mtyp.InvokeMember("PutCharArray", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; value|] ) 
    member t.PutFullMatrix (var: string) (xreal: double [,]) (ximag: double [,]) = mtyp.InvokeMember("PutFullMatrix", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; xreal; ximag|] ) 
    
    //
    // Use PutWorkspaceData to pass numeric and character array data respectively to the server. Do not use PutWorkspaceData on sparse arrays, structures, or function handles. Use the Execute method for these data types.
    //
    member t.PutWorkspaceData (var: string) (data: obj) = mtyp.InvokeMember("PutWorkspaceData", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; data|] ) 
        
type MatlabCommandExecutor(proxy: MatlabCOMProxy) =
    member t.GetVariables() = proxy.Execute [|"whos"|] :?> string |> MatlabHelpers.parseWhos

[<TypeProvider>]
type MatlabCOMProvider (config: TypeProviderConfig) as this = 
    inherit TypeProviderForNamespaces()
    let rootNamespace = "Matlab.COMTypeProvider"
    let thisAssembly  = Assembly.GetExecutingAssembly()
    let baseTy = typeof<obj>
    let matlabTy = ProvidedTypeDefinition(thisAssembly, rootNamespace, "MatlabType", Some baseTy)

    let staticParams = [ProvidedStaticParameter("instance", typeof<string>)]
    let mlKind = ref ""
    do matlabTy.DefineStaticParameters(
        staticParameters = staticParams, 
        apply = fun typeName paramValues ->
            match paramValues with
            | [| :? string as matlabKind |] -> mlKind := matlabKind
            matlabTy
        )

    let proxy = MatlabCOMProxy(!mlKind)
    let executor = MatlabCommandExecutor(proxy)

    let ty = ProvidedTypeDefinition(thisAssembly, rootNamespace, "MatlabContext", Some(baseTy))
    let ctor = ProvidedConstructor(parameters = [], InvokeCode = fun args -> <@@ "Data" :> obj @@>)
    do ty.AddMember(ctor)

    let tyData = ProvidedTypeDefinition(thisAssembly, rootNamespace, "MatlabDataContext", Some(baseTy))
    do tyData.AddMembersDelayed( fun () ->
            let vars = executor.GetVariables()
            [ 
                for var in vars do
                    let p = ProvidedProperty(propertyName = var.Name, propertyType = typeof<string>, IsStatic = false, GetterCode = fun _ -> <@@ var.Name @@>)
                    p.AddXmlDocDelayed(fun () -> sprintf "%A" var)
                    yield p
            ]
        )
    do ty.AddMemberDelayed(fun () -> tyData)
    do this.AddNamespace(rootNamespace, [ty])

[<assembly:TypeProviderAssembly>] 
do()