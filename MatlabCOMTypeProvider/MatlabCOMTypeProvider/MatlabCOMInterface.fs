module FSMatlab.COMInterface

open System
open System.Reflection
open System.Numerics

type MatlabCOMProxy (progid: string) =
    do if progid = "" then failwith "Empty progid unexpected"

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
    
//    member t.GetFullMatrix<'T> (var: string) (xsize: int) (ysize: int) = 
//        let xreal : double [,] ref = Array2D.zeroCreate xsize ysize |> ref
//        let ximag : double [,] ref = Array2D.zeroCreate xsize ysize |> ref
//        do mtyp.InvokeMember("GetFullMatrix", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; xreal; ximag |] ) |> ignore
//        xreal, ximag  
    
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

type MatlabVariable = {
        Name: string
        Size: int list
        Bytes: uint64
        Class: string
        Attributes: string list
    }

open Microsoft.Win32

module MatlabHelpers = 
    let nullToOption =
        function
        | null -> None
        | x -> Some x
    let optionReplace (y: Option<_>) (x: Option<_>) = 
        match x with
        | Some x -> Some x
        | None -> y 

    let getProgIDsAndPaths () = 
        use regClsid = Registry.ClassesRoot.OpenSubKey("CLSID") in
            regClsid.GetSubKeyNames()
            |> Array.map (fun clsid -> 
                use key = regClsid.OpenSubKey(clsid) in 
                    key.OpenSubKey("ProgID"), key.OpenSubKey("InprocServer32"), key.OpenSubKey("LocalServer32"))
            |> Array.map (fun (progid, ipspath, lspath) -> progid, if ipspath = null then lspath else ipspath)
            |> Array.filter (fun (progid, path) -> progid <> null && path <> null) 
            |> Array.map (fun (progid, path) -> let res = progid.GetValue(""), path.GetValue("") in progid.Close(); path.Close(); res)
            |> Array.filter (fun (pid, pth) -> pid <> null && pth <> null)
            |> Array.map (fun (pid, pth) -> (string pid) + " -> " + (string pth))

    let getProgIDs () = 
        use regClsid = Registry.ClassesRoot.OpenSubKey("CLSID") in
            regClsid.GetSubKeyNames()
            |> Array.map (fun clsid -> use key = regClsid.OpenSubKey(clsid) in key.OpenSubKey("ProgID"))            
            |> Array.filter ((<>) null)
            |> Array.map (fun (progid) -> let spid = progid.GetValue("") in progid.Close(); spid)
            |> Array.filter ((<>) null)
            |> Array.map (string)

    let getMatlabProgIDs () = 
        getProgIDs () 
        |> Array.filter (fun progid -> progid.Contains("Matlab"))
        
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

    let (|MComplex|_|) =
        function 
        | { Attributes = ["complex"] } -> Some ()
        | _ -> None

    let (|MString|MDouble|MVector|MMatrix|MUnexpected|) =
        function
        | { Size = [1; _]; Class = "char"   } -> MString
        | { Size = [1; 1]; Class = "double" } -> MDouble
        | { Size = [1; _]; Class = "double" } -> MVector
        | { Size = [_; _]; Class = "double" } -> MMatrix
        | _ -> MUnexpected

open MatlabHelpers

type MatlabCommandExecutor(proxy: MatlabCOMProxy) =
    member t.GetVariableInfos() = proxy.Execute [|"whos"|] :?> string |> parseWhos
    member t.GetVariableType (v: MatlabVariable) = 
        match v with
        | MString -> typeof<string>
        | MDouble & MComplex -> typeof<Complex>
        | MDouble            -> typeof<double>
        | MVector & MComplex -> typeof<Complex []>
        | MVector            -> typeof<double []>        
        | MMatrix & MComplex -> typeof<Complex [,]>
        | MMatrix            -> typeof<double [,]>
        | MUnexpected -> failwith (sprintf "Could not figure out type for Unexpected/Unsupported Variable Type: %A" v)
    member t.GetVariableContents (v: MatlabVariable) = 
        match v with
        //| MComplex -> failwith "Accessing complex types is not yet supported"
        | MString -> proxy.GetCharArray(v.Name) :> obj
        | MDouble
        | MVector 
        | MMatrix -> proxy.GetVariable(v.Name)
        | MUnexpected -> failwith (sprintf "Could not read Unexpected/Unsupported Variable Type: %A" v)