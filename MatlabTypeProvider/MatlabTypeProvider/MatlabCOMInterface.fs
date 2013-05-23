module FSMatlab.COMInterface

type MatlabTypes = 
    | MString = 0
    | MDouble = 1
    | MVector = 2
    | MMatrix = 3
    | MComplexDouble = 4
    | MComplexVector = 5
    | MComplexMatrix = 6
    | MUnexpected = 7

type MatlabMethod = {
        Name: string
        InArgs: string list
        OutArgs: string list
        Access: string
        Static: bool
    }


type MatlabFunction = {
        Name: string
        InParams: string list
        OutParams: string list
        Path: string
    }

type MatlabVariable = {
        Name: string
        Size: int list
        Bytes: uint64
        Class: string
        Attributes: string list
    }

type MatlabToolbox = {
        Name: string
        Path: string
        HelpName: string option
        Funcs: MatlabFunction seq
    }

module MatlabFunctionHelpers = 
    open System
    open System.IO
    open Parsing 
    open FSMatlab.FunctionParsing

    let searchPathForFunctions (searchPath: string) : MatlabFunction seq =
        seq {                                         
            for file in Directory.EnumerateFiles(searchPath, "*.m") do
                let fullPath = Path.Combine(searchPath, file)
                let genEmptyFunc () = { Name = Path.GetFileNameWithoutExtension(file) ; InParams = [ "varargsin" ] ; OutParams = [ "varargsout" ] ; Path = fullPath }
                let mlFunc = 
                    try 
                        match StringWindow(File.ReadAllText(fullPath), 0u) |> findFunc |> Option.map (parseFunDecl) with
                        | Some (name, inparams, outparams) -> { Name = name; InParams = inparams; OutParams = outparams; Path = fullPath }
                        | None -> genEmptyFunc ()
                    with ex -> genEmptyFunc ()
                yield mlFunc
        }        

    let toolboxesFromPaths (matlabPath: string) (searchPaths: string seq) = 
        seq {
            let toolboxPath = Path.Combine(matlabPath, "toolbox")
            let userIdx = ref 0
            for searchPath in searchPaths do
                let name, helpname = 
                    if searchPath.StartsWith(toolboxPath) then // Actual Matlab Toolbox
                        let helpname = searchPath.Remove(0, toolboxPath.Length + 1)
                        let name = helpname.Replace(Path.PathSeparator, '_')
                        name, Some helpname
                    else // User Defined "Toolbox"
                        // TODO: Find a better way to name user toolboxes
                        do userIdx := !userIdx + 1
                        "User" + (string !userIdx), None
                yield { Name = name; Path = searchPath; HelpName = helpname; Funcs = searchPathForFunctions searchPath |> Seq.cache }
        }



module MatlabCallHelpers = 
    open System
    open Microsoft.Win32

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

    let getMatlabTypeFromMatlabSig =
        function
        | { Size = [1; _]; Class = "char"   }                            -> MatlabTypes.MString
        | { Size = [1; 1]; Class = "double"; Attributes = [ "complex" ]} -> MatlabTypes.MComplexDouble
        | { Size = [1; 1]; Class = "double" }                            -> MatlabTypes.MDouble
        | { Size = [1; _]; Class = "double"; Attributes = [ "complex" ]} -> MatlabTypes.MComplexVector
        | { Size = [1; _]; Class = "double" }                            -> MatlabTypes.MVector
        | { Size = [_; _]; Class = "double"; Attributes = [ "complex" ]} -> MatlabTypes.MComplexMatrix
        | { Size = [_; _]; Class = "double" }                            -> MatlabTypes.MMatrix
        | _ -> MatlabTypes.MUnexpected

    let getMatlabTypeFromDotNetSig =
        function
        | t when t = typeof<string> -> MatlabTypes.MString
        | t when t = typeof<double> -> MatlabTypes.MDouble
        | t when t = typeof<double []> -> MatlabTypes.MVector
        | t when t = typeof<double [,]> -> MatlabTypes.MMatrix
        | t when t = typeof<System.Numerics.Complex> -> MatlabTypes.MComplexDouble
        | t when t = typeof<System.Numerics.Complex []> -> MatlabTypes.MComplexVector
        | t when t = typeof<System.Numerics.Complex [,]> -> MatlabTypes.MComplexMatrix 
        | t -> failwith (sprintf "Unsupported Variable Type: %s" (t.ToString()))

    let getDotNetType = 
        function
        | MatlabTypes.MString            -> typeof<string>
        | MatlabTypes.MDouble            -> typeof<double>
        | MatlabTypes.MVector            -> typeof<double []>        
        | MatlabTypes.MMatrix            -> typeof<double [,]> 
        | MatlabTypes.MComplexDouble     -> typeof<System.Numerics.Complex>
        | MatlabTypes.MComplexVector     -> typeof<System.Numerics.Complex []>
        | MatlabTypes.MComplexMatrix     -> typeof<System.Numerics.Complex [,]>      
        | MatlabTypes.MUnexpected -> failwith (sprintf "Unsupported Variable Type")
        | _ -> failwith "Unexpected MatlabTypes enumeration value"

    let correctFEvalResult (res: obj) =
        match res with
        // One array (returned as [,])
        | :? Array as arr when (arr.Rank = 2 && arr.GetLength(0) = 1) -> 
            let copyBytes = System.Buffer.ByteLength(arr)
            let elemType = arr.GetType().GetElementType()
            let newArr = Array.CreateInstance(elemType, arr.GetLength(1))
            do System.Buffer.BlockCopy(arr, 0, newArr, 0, copyBytes)
            newArr :> obj
        | x -> x

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

    let addXMCDocMarkup (helpText: string) =   
        let splitChars = Environment.NewLine.ToCharArray()     
        let innerText = 
            helpText.Split(splitChars, StringSplitOptions.None) 
            |> Seq.map (fun str -> """<para>""" + str + """</para>""")
            |> Seq.reduce (+)
        """<summary>""" + innerText + """</summary>"""

    let parseHelp (pkgName: string) (helpText: string) =
        if helpText.Trim() = "" then None
        else helpText |> removeHtmlTags |> addXMCDocMarkup |> Some

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
    let getHelpString (topic: string) =
        """disp(help('""" + topic + """'))"""

open System
open System.Reflection
open System.Numerics

open MatlabCallHelpers
open MatlabCOM

type MatlabCommandExecutor(proxy: MatlabCOMProxy) =
    member t.GetFunctionSearchPaths () = proxy.Execute [|"disp(path)"|] :?> string |> (fun paths -> paths.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)) |> Array.map (fun str -> str.Trim())
    member t.GetRoot () = proxy.Execute [|"matlabroot"|] :?> string |> (fun str -> str.Trim())

    member t.GetPackageHelp (pkgName: string) = 
        match proxy.Execute [|MatlabStrings.getHelpString pkgName|] :?> string |> parseHelp pkgName with
        | Some (help) -> help
        | None -> "No help available for this Package"
    member t.GetMethodHelp (pkgName: string) (funcName: string) = 
        match let helpName = pkgName + "." + funcName in proxy.Execute [|MatlabStrings.getHelpString helpName|] :?> string |> parseHelp helpName with
        | Some (help) -> help
        | None -> "No help available for this method"
    member t.GetToolboxHelp (tb: MatlabToolbox) = 
        tb.HelpName |> Option.bind (fun helpName -> proxy.Execute [| MatlabStrings.getHelpString helpName |] :?> string |> parseHelp helpName)
        |> function | Some (help) -> help | None -> "Toolbox Path: " + tb.Path + Environment.NewLine + "No help available for this toolbox"
    member t.GetFunctionHelp (tb: MatlabToolbox) (f: MatlabFunction) =  
        tb.HelpName |> Option.bind (fun tbHelpName -> let helpName = tbHelpName + "\\" + f.Name in proxy.Execute [|MatlabStrings.getHelpString helpName|] :?> string |> parseHelp helpName)
        |> function | Some (help) -> help | None -> "Function Path: " + f.Path + Environment.NewLine + "No help available for this function"

    member t.GetPackageNames() = proxy.Execute [|"strjoin(cellfun(@(x) x.Name, meta.package.getAllPackages(), 'UniformOutput', false)', ';')"|] 
                                 :?> string |> parsePackages |> filterPackages
    member t.GetPackageFunctions (pkgName: string) = proxy.Execute [|MatlabStrings.getPackageFunctions pkgName|] :?> string |> tsvToFuncitonInfo

    member t.GetVariableInfos() = proxy.Execute [|"whos"|] :?> string |> parseWhos
    member t.GetVariableInfo name = proxy.Execute [|"whos " + name|] :?> string |> parseWhos |> Array.tryFind (fun _ -> true)

    member t.GetVariableMatlabType (v: MatlabVariable) = getMatlabTypeFromMatlabSig v
    member t.GetVariableDotNetType (v: MatlabVariable) = getDotNetType (getMatlabTypeFromMatlabSig v)

    member t.CallFunction (name: string) (numout: int) (args: obj []) (hasVarargin: bool) (hasVarargout: bool) : obj = 
        let expandedArgs = 
            if hasVarargin then 
                match args.[args.Length - 1] with
                | :? (obj []) as varargin -> let nvar = (Array.sub args 0 (args.Length - 1)) in Array.append nvar varargin
                | v -> args
            else args 
            
        match proxy.Feval name numout expandedArgs with 
        | :? (obj []) as arrayRes -> 
            match arrayRes |> Array.map correctFEvalResult with
            | arrayRes when arrayRes.Length = 1 -> arrayRes.[0]
            | arrayRes when arrayRes.Length <= 8 ->
                let tupleType = Microsoft.FSharp.Reflection.FSharpType.MakeTupleType(Array.create arrayRes.Length typeof<obj>)
                Microsoft.FSharp.Reflection.FSharpValue.MakeTuple (arrayRes, tupleType)
            | arrayRes -> arrayRes :> obj
        | unexpected -> failwith (sprintf "Unexpected type returned from Feval: %s" (unexpected.GetType().ToString()))

    member t.GetVariableContents (vname: string) (vtype: MatlabTypes) = 
        match vtype with
        | MatlabTypes.MString     -> proxy.GetCharArray(vname) :> obj
        | MatlabTypes.MDouble     -> proxy.GetVariable(vname) 
        | MatlabTypes.MVector     -> 
            match t.GetVariableInfo(vname) with
            | Some { Size = [1; sy] } -> 
                let real, _ = proxy.GetFullMatrix(vname, 1, sy, hasImag = true) 
                let carr = Array.CreateInstance(typeof<double>, sy) :?> double []
                for i = 0 to sy - 1 do Array.set carr i (real.[0,i])
                carr :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a vector" vname)
        | MatlabTypes.MMatrix     -> 
            match t.GetVariableInfo(vname) with
            | Some { Size = [sx;sy] } -> proxy.GetFullMatrix(vname,sx,sy) |> fst :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a matrix" vname)
        | MatlabTypes.MComplexDouble     -> 
            let r, i = proxy.GetFullMatrix(vname,1,1,true) |> fun (r,i) -> r.[0,0], i.[0,0]
            System.Numerics.Complex(r, i) :> obj
        | MatlabTypes.MComplexVector     ->
            match t.GetVariableInfo(vname) with
            | Some { Size = [1; sy]; Attributes = ["complex"] } -> 
                let real, imag = proxy.GetFullMatrix(vname, 1, sy, hasImag = true) 
                let carr = Array.CreateInstance(typeof<System.Numerics.Complex>, sy) :?> Complex [] 
                for i = 0 to sy - 1 do Array.set carr i (Complex(real.[0,i], imag.[0,i]))
                carr :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a complex vector" vname)         
        | MatlabTypes.MComplexMatrix     -> 
            match t.GetVariableInfo(vname) with
            | Some { Size = [sx;sy]; Attributes = ["complex"] } -> 
                let real, imag = proxy.GetFullMatrix(vname, sx, ysize = sy, hasImag = true) 
                let carr = Array.CreateInstance(typeof<System.Numerics.Complex>, sx, sy) :?> Complex [,] 
                for i = 0 to sx - 1 do
                    for j = 0 to sy - 1 do
                         Array2D.set<System.Numerics.Complex> carr i j (Complex(real.[i,j], imag.[i,j]))
                carr :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a complex matrix" vname)         
        | _ -> failwith (sprintf "Could not read Unexpected/Unsupported Variable Type: %A" vtype)