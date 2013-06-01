module FSMatlab.Interface
open FSMatlab.InterfaceTypes

module MatlabFunctionHelpers = 
    open System
    open System.IO
    open ParsingHelpers
    open FSMatlab.FunctionParsing

    let pathToFunctionInfo (fullPath: string) : MatlabFunctionInfo =
        let genEmptyFunc () = { Name = Path.GetFileNameWithoutExtension(fullPath) ; InParams = [ "varargin" ] ; OutParams = [ "varargout" ] ; Path = fullPath }
        let mlFunc = 
            try 
                match StringWindow(File.ReadAllText(fullPath), 0u) |> findFunc |> Option.map (parseFunDecl) with
                | Some (name, inparams, outparams) -> { Name = name; InParams = inparams; OutParams = outparams; Path = fullPath }
                | None -> genEmptyFunc ()
            with ex -> genEmptyFunc ()
        mlFunc

    let searchPathForFunctions (searchPath: string) : MatlabFunctionInfo seq =
        seq {                                         
            for file in Directory.EnumerateFiles(searchPath, "*.m") do
                let fullPath = Path.Combine(searchPath, file)
                yield pathToFunctionInfo fullPath
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
    open System.Text
    open Microsoft.Win32
    
    let processId = lazy (System.Diagnostics.Process.GetCurrentProcess().Id.ToString())

    let getSafeRandomVariableName (currentVariables: string []) =
        let varNames =  seq {
                            let procid = processId.Force()
                            let randomAddendum = System.IO.Path.GetRandomFileName().Replace('.', '_')
                            yield System.String.Format("mtp_{0}_{1}", procid, randomAddendum)
                        }
        varNames |> Seq.find (fun v -> not <| Array.exists (fun cv -> cv = v) currentVariables)

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


    let correctFEvalResult (res: obj) =
        match res with
        // Fast(ish) for Value Type Arrays
        | :? Array as arr when (arr.Rank = 2 && arr.GetLength(0) = 1 && arr.GetLength(1) > 0
                                && arr.GetType().GetElementType().IsValueType) -> 
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

    let tsvToMethodInfo (tsv: string) = 
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

    let commaDelm (strs: string []) = 
        let sb = new StringBuilder()
        for i = 0 to strs.Length - 1 do
            sb.Append(strs.[i]) |> ignore
            if i <> strs.Length - 1 then sb.Append(",") |> ignore
        sb.ToString()        

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



module TypeConverters =
    let getMatlabTypeFromMatlabSig =
        function
        | { Size = [1; _]; Class = "char"   }                            -> MatlabType.MString
        | { Size = [1; 1]; Class = "double"; Attributes = [ "complex" ]} -> MatlabType.MComplexDouble
        | { Size = [1; 1]; Class = "double" }                            -> MatlabType.MDouble
        | { Size = [1; _]; Class = "double"; Attributes = [ "complex" ]} -> MatlabType.MComplexVector
        | { Size = [1; _]; Class = "double" }                            -> MatlabType.MVector
        | { Size = [_; _]; Class = "double"; Attributes = [ "complex" ]} -> MatlabType.MComplexMatrix
        | { Size = [_; _]; Class = "double" }                            -> MatlabType.MMatrix
        | _ -> MatlabType.MUnexpected

    let getMatlabTypeFromDotNetSig =
        function
        | t when t = typeof<string> -> MatlabType.MString
        | t when t = typeof<double> -> MatlabType.MDouble
        | t when t = typeof<double []> -> MatlabType.MVector
        | t when t = typeof<double [,]> -> MatlabType.MMatrix
        | t when t = typeof<System.Numerics.Complex> -> MatlabType.MComplexDouble
        | t when t = typeof<System.Numerics.Complex []> -> MatlabType.MComplexVector
        | t when t = typeof<System.Numerics.Complex [,]> -> MatlabType.MComplexMatrix 
        | t -> failwith (sprintf "Unsupported Variable Type: %s" (t.ToString()))

    let getDotNetType = 
        function
        | MatlabType.MString            -> typeof<string>
        | MatlabType.MDouble            -> typeof<double>
        | MatlabType.MVector            -> typeof<double []>        
        | MatlabType.MMatrix            -> typeof<double [,]> 
        | MatlabType.MComplexDouble     -> typeof<System.Numerics.Complex>
        | MatlabType.MComplexVector     -> typeof<System.Numerics.Complex []>
        | MatlabType.MComplexMatrix     -> typeof<System.Numerics.Complex [,]>      
        | MatlabType.MUnexpected -> failwith (sprintf "Unsupported Variable Type")
        | _ -> failwith "Unexpected MatlabTypes enumeration value"

open System
open System.Reflection
open System.Numerics
open System.Text

open MatlabCallHelpers
open MatlabCOM

module RepresentationBuilders =     
    let getVariableHandleFromVariableInfo (info: MatlabVariableInfo) (getContents: string -> MatlabType -> obj) (deleteVar: string -> unit) : IMatlabVariableHandle =
        let matlabType = TypeConverters.getMatlabTypeFromMatlabSig(info)
        { new IMatlabVariableHandle with
            member t.Name = info.Name 
            member t.GetUntyped () = getContents info.Name  matlabType 
            member t.Info = info
            member t.MatlabType = matlabType
            member t.LocalType = TypeConverters.getDotNetType(matlabType)  
            member t.Delete () = deleteVar info.Name
        }

    let getFunctionHandleFromFunctionInfo (info: MatlabFunctionInfo) (execFuncNamed: obj[] -> string[] -> IMatlabVariableHandle[]) (execFuncNum: obj[] -> int -> IMatlabVariableHandle[])  : IMatlabFunctionHandle =
        { new IMatlabFunctionHandle with
            member t.Name = info.Name
            member t.Apply (args: obj[]) = 
                { new IMatlabAppliedFunctionHandle with
                    member t.Name = info.Name
                    member t.Execute (outVars: string []) = execFuncNamed args outVars        
                    member t.Execute (numOutVars: int) = execFuncNum args numOutVars
                    member t.Info = info 
                }
            member t.Info = info 
        }

type MatlabCommandExecutor(proxy: MatlabCOMProxy) as this =

    //
    // Actual Useful Stuff
    //

    member t.GetPackageHelp (pkgName: string) = 
        match proxy.Execute [|MatlabStrings.getHelpString pkgName|] :?> string |> parseHelp pkgName with
        | Some (help) -> help
        | None -> "No help available for this Package"
 
    member t.GetMethodHelp (pkgName: string) (funcName: string) = 
        match let helpName = pkgName + "." + funcName in proxy.Execute [|MatlabStrings.getHelpString helpName|] :?> string |> parseHelp helpName with
        | Some (help) -> help
        | None -> "No help available for this method"
 
    member t.GetToolboxHelp (tb: MatlabToolboxInfo) = 
        tb.HelpName |> Option.bind (fun helpName -> proxy.Execute [| MatlabStrings.getHelpString helpName |] :?> string |> parseHelp helpName)
        |> function | Some (help) -> help | None -> "Toolbox Path: " + tb.Path + Environment.NewLine + "No help available for this toolbox"

    member t.GetFunctionHelp (tb: MatlabToolboxInfo) (f: MatlabFunctionInfo) =  
        tb.HelpName |> Option.bind (fun tbHelpName -> let helpName = tbHelpName + "\\" + f.Name in proxy.Execute [|MatlabStrings.getHelpString helpName|] :?> string |> parseHelp helpName)
        |> function | Some (help) -> help | None -> "Function Path: " + f.Path + Environment.NewLine + "No help available for this function"

    member t.GetPackageNames() =
         proxy.Execute [|"strjoin(cellfun(@(x) x.Name, meta.package.getAllPackages(), 'UniformOutput', false)', ';')"|] 
         :?> string |> parsePackages |> filterPackages

    member t.GetPackageMethods (pkgName: string) = 
        proxy.Execute [|MatlabStrings.getPackageFunctions pkgName|] :?> string |> tsvToMethodInfo

    member t.GetToolboxes () = 
        let matlabRoot = proxy.Execute [|"matlabroot"|] :?> string |> (fun str -> str.Trim())
        let searchPaths = proxy.Execute [|"disp(path)"|] :?> string |> (fun paths -> paths.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)) |> Array.map (fun str -> str.Trim())
        MatlabFunctionHelpers.toolboxesFromPaths matlabRoot searchPaths

    member t.GetFunctionHandle (funcInfo: MatlabFunctionInfo) =
        let fexecNamed (inargs: obj []) (outargs: string []) = t.CallFunctionWithHandles(funcInfo.Name, outargs, inargs)
        let fexecNum (inargs: obj []) (out: int) = t.CallFunctionWithHandles(funcInfo.Name, out, inargs)
        RepresentationBuilders.getFunctionHandleFromFunctionInfo funcInfo fexecNamed fexecNum

    member t.GetFunctionInfoFromFile (path: string) = MatlabFunctionHelpers.pathToFunctionInfo path   
    member t.GetVariableInfos() = proxy.Execute [|"whos"|] :?> string |> parseWhos
    member t.GetVariableInfo name = proxy.Execute [|"whos " + name|] :?> string |> parseWhos |> Array.tryFind (fun _ -> true)

    member t.SetVariable(name: string, value: obj) : IMatlabVariableHandle =
        // TODO: Proper Conversions
        proxy.PutWorkspaceData name value
        t.GetVariableHandle(name)

    member t.DeleteVariable(name: string) : unit = 
        proxy.Execute([|"clear " + name|]) |> ignore

    member t.GetVariableHandle(info: MatlabVariableInfo) =
        let getFunc name mltype = t.GetVariableContents(name, mltype)
        let deleteFunc name = t.DeleteVariable(name)
        RepresentationBuilders.getVariableHandleFromVariableInfo info getFunc deleteFunc       

    member t.GetVariableHandle(name: string) = 
        let argInfo = match t.GetVariableInfo(name) with
                    | Some (name) -> name
                    | None -> failwith (sprintf "Variable not found: %s" name)
        t.GetVariableHandle(argInfo)

    /// Call a function with either MatlabVariableHandles or Objs, Returns a handle
    /// Note: Will create temporary variables with random names on the matlab side
    member t.CallFunctionWithHandles (name: string, outArgNames: string [], args: obj []) : IMatlabVariableHandle [] = 
        // Push non-handle variables to matlab, and keep track of which were pushed just for this function
        let currentVars = lazy (t.GetVariableInfos() |> Array.map (fun vi -> vi.Name))
        let inArgs =
            [| for arg in args do 
                    yield  match arg with 
                           | :? IMatlabVariableHandle as h -> h, false
                           | o -> 
                                let varname = getSafeRandomVariableName (currentVars.Force()) 
                                t.SetVariable(varname, o),true // Side Effect: Sets variables matlab side
            |]   

        // Generate call text
              
        let formattedOutArgs = commaDelm outArgNames
        let formattedInArgs = commaDelm (inArgs |> Array.map (fun (f,s) -> f.Name))
        let formattedCall = String.Format("[{0}] = {1}({2})", formattedOutArgs, name, formattedInArgs)

        // Make call
        let res = proxy.Execute([|formattedCall|]) 
        // TODO: Check res for errors (it may just throw)

        // Delete inargs variables that were generated just for this call
        for arg, deleteMe in inArgs do
            if deleteMe then proxy.Execute([|String.Format("clear {0}", arg)|]) |> ignore

        // Build result handles
        [| for outArgName in outArgNames do yield t.GetVariableHandle(outArgName) |]

    /// Just like the other CallFunctionWithHandles but will use randomized result value names 
    member t.CallFunctionWithHandles (name: string, numout: int, args: obj []) : IMatlabVariableHandle [] = 
        let currentVars = lazy (t.GetVariableInfos() |> Array.map (fun vi -> vi.Name))
        let outargs = Array.init numout (fun _ -> getSafeRandomVariableName (currentVars.Force()))
        t.CallFunctionWithHandles(name, outargs, args)

    /// Old style, will transform output appropriately but no handles allowed 
    member t.CallFunctionWithValues (name: string, numout: int, namedArgs: obj [], varArgs: obj [], hasVarArgsOut: bool) : obj = 
        let actualArgs = Array.append namedArgs varArgs
        //failwith (sprintf "%s: %A (%s) (%s) -> %i" name actualArgs (actualArgs.GetType().ToString()) (actualArgs.GetType().GetElementType().ToString()) numout)
        match proxy.Feval name numout actualArgs with 
        | :? (obj []) as arrayRes when not hasVarArgsOut -> 
            match arrayRes |> Array.map correctFEvalResult with
            | arrayRes when arrayRes.Length = 1 -> arrayRes.[0]
            | arrayRes when arrayRes.Length <= 8 ->
                let tupleType = Microsoft.FSharp.Reflection.FSharpType.MakeTupleType(Array.create arrayRes.Length typeof<obj>)
                Microsoft.FSharp.Reflection.FSharpValue.MakeTuple (arrayRes, tupleType)
            | arrayRes -> arrayRes :> obj       
        | :? (obj []) as arrayRes when hasVarArgsOut -> arrayRes |> Array.map correctFEvalResult :> obj
        | unexpected -> failwith (sprintf "Unexpected type returned from Feval: %s" (unexpected.GetType().ToString()))

    member t.GetVariableContents (vname: string, vtype: MatlabType) = 
        match vtype with
        | MatlabType.MString     -> proxy.GetCharArray(vname) :> obj
        | MatlabType.MDouble     -> proxy.GetVariable(vname) 
        | MatlabType.MVector     -> 
            match t.GetVariableInfo(vname) with
            | Some { Size = [1; sy] } ->  let real, _ = proxy.GetFullMatrix(vname, 1, sy, hasImag = false) 
                                          let carr = Array.CreateInstance(typeof<double>, sy) :?> double []
                                          for i = 0 to sy - 1 do Array.set carr i (real.[0,i])
                                          carr :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a vector" vname)
        | MatlabType.MMatrix     -> 
            match t.GetVariableInfo(vname) with
            | Some { Size = [sx;sy] } -> proxy.GetFullMatrix(vname,sx,sy) |> fst :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a matrix" vname)
        | MatlabType.MComplexDouble     -> 
            let r, i = proxy.GetFullMatrix(vname,1,1,true) |> fun (r,i) -> r.[0,0], i.[0,0]
            Complex(r, i) :> obj
        | MatlabType.MComplexVector     ->
            match t.GetVariableInfo(vname) with
            | Some { Size = [1; sy]; Attributes = ["complex"] } -> 
                let real, imag = proxy.GetFullMatrix(vname, 1, sy, hasImag = true) 
                let carr = Array.CreateInstance(typeof<System.Numerics.Complex>, sy) :?> Complex [] 
                for i = 0 to sy - 1 do carr.[i] <- (Complex(real.[0,i], imag.[0,i]))
                carr :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a complex vector" vname)         
        | MatlabType.MComplexMatrix     -> 
            match t.GetVariableInfo(vname) with
            | Some { Size = [sx;sy]; Attributes = ["complex"] } -> 
                let real, imag = proxy.GetFullMatrix(vname, sx, ysize = sy, hasImag = true) 
                let carr = Array.CreateInstance(typeof<System.Numerics.Complex>, sx, sy) :?> Complex [,] 
                for i = 0 to sx - 1 do
                    for j = 0 to sy - 1 do
                        carr.[i,j] <- (Complex(real.[i,j], imag.[i,j]))
                carr :> obj
            | _ -> failwith (sprintf "Variable %s does not exist or is not a complex matrix" vname)         
        | _ -> failwith (sprintf "Could not read Unexpected/Unsupported Variable Type: %A" vtype)


