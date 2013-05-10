module FSMatlab.COMInterface

open System
open System.Reflection
open System.Numerics

type MatlabCOMProxy (progid: string) as this =
    do if progid = "" then failwith "Empty progid unexpected"

    let mtyp = Type.GetTypeFromProgID( progid ) //"Matlab.Desktop.Application"
    let ml = Activator.CreateInstance(mtyp)

    let removeAns (resultStr: string) =
        // Answer string header: [|'\010'; 'a'; 'n'; 's'; ' '; '='; '\010'; '\010';|]
        let ansStr = String([|'\010'; 'a'; 'n'; 's'; ' '; '='; '\010'; '\010'|])
        if resultStr.StartsWith(ansStr) then resultStr.Remove(0, ansStr.Length) else resultStr

    do this.Execute([|"disp('F# Proxy is Connected.')"|]) |> ignore
    //
    // The server returns output from the command in the string, result. The result string also contains any warning or error messages that might have been issued by MATLAB software as a result of the command.
    //
    member t.Execute (args: obj[]) = 
        match mtyp.InvokeMember("Execute", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, args) with
        | :? string as strres -> removeAns strres :> obj
        | other -> other

    //
    // Feval('functionname',numout,arg1,arg2,...) 
    //
    member t.Feval (name: string) (outparams: int) (args: obj []) = 
        mtyp.InvokeMember("Feval", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, Array.append [|string; outparams|] args)
    
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
    
    member t.GetFullMatrix (var: string) = 
        let mutable xreal : double [,] = null
        let mutable ximag : double [] =  null
        do mtyp.InvokeMember("GetFullMatrix", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; xreal; ximag |] ) |> ignore
        xreal, ximag  
    
    ///
    /// Use GetWorkspaceData instead of GetFullMatrix and GetCharArray to get numeric and character array data, respectively. Do not use GetWorkspaceData on sparse arrays, structures, or function handles.
    /// These functions use the variant data type instead of the safearray data type used by GetFullMatrix and PutFullMatrix.
    ///
    member t.GetWorkspaceData (var: string) = 
        let mutable res : obj = null
        do mtyp.InvokeMember("GetWorkspaceData", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base", res|] ) |> ignore
        res

    member t.PutCharArray (var:string) (value:string) =  mtyp.InvokeMember("PutCharArray", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; value|] ) 
    member t.PutFullMatrix (var: string) (xreal: double [,]) (ximag: double [,]) = mtyp.InvokeMember("PutFullMatrix", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; xreal; ximag|] ) 
    
    //
    // Use PutWorkspaceData to pass numeric and character array data respectively to the server. Do not use PutWorkspaceData on sparse arrays, structures, or function handles. Use the Execute method for these data types.
    //
    member t.PutWorkspaceData (var: string) (data: obj) = mtyp.InvokeMember("PutWorkspaceData", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; data|] ) 

type MatlabTypes = 
    | MString = 0
    | MDouble = 1
    | MVector = 2
    | MMatrix = 3
    | MUnexpected = 4

type MatlabFunction = {
    Name: string
    InArgs: string list
    OutArgs: string list
    Access: string
    Static: bool
    }

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

    let getMatlabType =
        function
        | { Size = [1; _]; Class = "char"   } -> MatlabTypes.MString
        | { Size = [1; 1]; Class = "double" } -> MatlabTypes.MDouble
        | { Size = [1; _]; Class = "double" } -> MatlabTypes.MVector
        | { Size = [_; _]; Class = "double" } -> MatlabTypes.MMatrix
        | _ -> MatlabTypes.MUnexpected

    let parsePackages (pkgs: string) =
        pkgs.Split([|';'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList |> List.map (fun pkg -> pkg.Trim())

    let filterPackages (pkgs: string list) =
        let badPkgs = [| "MS"; "Microsoft"; "System" |]
        pkgs |> List.filter (fun p -> (badPkgs |> Array.forall (fun bp -> not <| p.StartsWith(bp))))

    let tsvToFuncitonInfo (tsv: string) = 
        try
            [
                for line in tsv.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries) do
                    let fields = line.Split([|'\t'|], StringSplitOptions.None)
                    yield {
                        Name = fields.[0]
                        InArgs = fields.[1].Split([|';'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
                        OutArgs = fields.[2].Split([|';'|], StringSplitOptions.RemoveEmptyEntries) |> Array.toList
                        Access = fields.[3]
                        Static = fields.[4] = "1"
                    }
            ]
        with _ -> [ { Name = "Error"; InArgs = []; OutArgs = []; Access = "public"; Static = true }]

    let removeHtmlTags (html: string) =
        System.Text.RegularExpressions.Regex.Replace(html, @"<(.|\n)*?>", String.Empty)

    let parseHelp (pkgName: string) (helpText: string) =
        if helpText.StartsWith(Environment.NewLine + pkgName + "not found.") then "No Matlab documentation found."
        else helpText |> removeHtmlTags

    
    open Parsing
    open System

    let rec findDeclariation (window: StringWindow) =
        [
            match window.WindowAfterIndexOf("function") with
            | Some window ->                
                let eqIdx = window.IndexOf("=") 
                let codomainPrms = window.Substring(eqIdx).Split([|'[';']';',';' '|], StringSplitOptions.RemoveEmptyEntries)

                let eqWindow = window.Subwindow(uint32 eqIdx + 1u)
                let varsStartIdx = eqWindow.IndexOf("(")
                let funName = eqWindow.Substring(varsStartIdx)

                let doWindow = eqWindow.Subwindow(uint32 varsStartIdx + 1u)
                let domainEndIdx = eqWindow.IndexOf(")")
                let domainPrms = doWindow.Substring(domainEndIdx).Split([|'(';')';',';' '|], StringSplitOptions.RemoveEmptyEntries)
                
                yield funName, domainPrms, codomainPrms
                yield! findDeclariation doWindow                                            
            | None -> ()
        ]
            
module MatlabStrings =
    let getPackageFunctions (pkgName: string) =
     """strjoin( transpose (...
                arrayfun (@(x) strjoin( ...
                                {x.Name, ...
                                 strjoin(transpose(x.InputNames), ';'), ...
                                 strjoin(transpose(x.OutputNames), ';'), ...
                                 sprintf('%d', x.Static), ...
                                 x.Access}, '\t'), ...
                             meta.package.fromName('""" + pkgName + """').FunctionList, 'UniformOutput', false) ...                 
                ), '\r')"""


open MatlabHelpers

type MatlabCommandExecutor(proxy: MatlabCOMProxy) =
    member t.GetPackageHelp (pkgName: string) = proxy.Execute [|"help " + pkgName|] :?> string |> parseHelp pkgName
    member t.GetFunctionHelp (pkgName: string) (funcName: string) = 
        let helpName = pkgName + "." + funcName in proxy.Execute [|"help " + helpName|] :?> string |> parseHelp helpName
    member t.GetPackageNames() = proxy.Execute [|"strjoin(cellfun(@(x) x.Name, meta.package.getAllPackages(), 'UniformOutput', false)', ';')"|] 
                                 :?> string |> parsePackages |> filterPackages
    member t.GetPackageFunctions (pkgName: string) = proxy.Execute [|MatlabStrings.getPackageFunctions pkgName|] :?> string |> tsvToFuncitonInfo
    member t.GetVariableInfos() = proxy.Execute [|"whos"|] :?> string |> parseWhos
    member t.GetVariableInfo name = proxy.Execute [|"whos " + name|] :?> string |> parseWhos |> Array.tryFind (fun _ -> true)
    member t.GetVariableMatlabType (v: MatlabVariable) = getMatlabType v
    member t.GetVariableDotNetType (v: MatlabVariable) = 
        match getMatlabType v with
        | MatlabTypes.MString -> typeof<string>
        | MatlabTypes.MDouble            -> typeof<double>
        | MatlabTypes.MVector            -> typeof<double [,]>        
        | MatlabTypes.MMatrix            -> typeof<double [,]>       
        | MatlabTypes.MUnexpected -> failwith (sprintf "Could not figure out type for Unexpected/Unsupported Variable Type: %A" v)
        | _ -> failwith "Unexpected MatlabTypes enumeration value"
    member t.CallFunction (name: string) (args: obj []) = proxy.Feval name 1 args

    member t.GetVariableContents (vname: string) (vtype: MatlabTypes) = 
        match vtype with
        //| MComplex -> failwith "Accessing complex types is not yet supported"
        | MatlabTypes.MString     -> proxy.GetCharArray(vname) :> obj
        | MatlabTypes.MDouble     -> proxy.GetVariable(vname) 
        | MatlabTypes.MVector     -> proxy.GetVariable(vname) 
        | MatlabTypes.MMatrix     -> 
            match t.GetVariableInfo(vname) with
            | Some { Size = [sx;sy] } -> 
                let mat = Array2D.zeroCreate<double> sx sy
                let arr = proxy.GetVariable(vname) :?> Array
                Array.Copy(arr, mat, arr.LongLength)
                mat :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a matrix" vname)
        | _ -> failwith (sprintf "Could not read Unexpected/Unsupported Variable Type: %A" vtype)