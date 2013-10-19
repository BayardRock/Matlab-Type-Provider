module InterfaceHelpers

open FSMatlab.InterfaceTypes

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
        | { Size = [1; 1]; Class = "logical" }                           -> MatlabType.MLogical
        | { Size = [1; _]; Class = "logical" }                           -> MatlabType.MLogicalVector
        | { Size = [_; _]; Class = "logical" }                           -> MatlabType.MLogicalMatrix
        | _ -> MatlabType.MUnexpected

    let getDotNetType = 
        function
        | MatlabType.MString            -> typeof<string>
        | MatlabType.MDouble            -> typeof<double>
        | MatlabType.MVector            -> typeof<double []>        
        | MatlabType.MMatrix            -> typeof<double [,]> 
        | MatlabType.MComplexDouble     -> typeof<System.Numerics.Complex>
        | MatlabType.MComplexVector     -> typeof<System.Numerics.Complex []>
        | MatlabType.MComplexMatrix     -> typeof<System.Numerics.Complex [,]>  
        | MatlabType.MLogical           -> typeof<bool>
        | MatlabType.MLogicalVector     -> typeof<bool []>
        | MatlabType.MLogicalMatrix     -> typeof<bool [,]>
        | MatlabType.MUnexpected        -> typeof<obj>
        | unexpectedType -> failwith ("Unspecified MatlabTypes enumeration value: " + System.Enum.GetName(typeof<MatlabType>, unexpectedType))

    let constructTypeProviderVariableInfo (mvi: MatlabVariableInfo) =
        let mltype = getMatlabTypeFromMatlabSig mvi
        let dntype = 
            try getDotNetType mltype
            with ex -> failwith (ex.Message + ": " + mvi.Class + " of " + mvi.Size.ToString())
        {
            MatlabVariableInfo = mvi
            MatlabType = mltype
            Type = dntype
        } 
        

module MatlabFunctionHelpers = 
    open System
    open System.IO
    open ParsingHelpers
    open FSMatlab.FunctionParsing

    let fileToFunctionInfo (fullPath: string) : MatlabFunctionInfo option =
        try 
            match StringWindow(File.ReadAllText(fullPath), 0u) |> findFunc |> Option.map (parseFunDecl) with
            | Some (name, inparams, outparams) -> Some { Name = name; InParams = inparams; OutParams = outparams; Path = Some fullPath }
            | None -> None 
        with ex -> None

    let searchPathForFunctionFiles (searchPath: string) : string seq =
        seq {                                         
            for file in Directory.EnumerateFiles(searchPath, "*.m") do
                yield Path.Combine(searchPath, file)
        }

    let rec decomposePath (root: string) (path: string) =
        [
            if path.StartsWith root && not (path = root) then
                let rmpath = path.Remove(0, root.Length + 1) in 
                    yield! rmpath.Split([|Path.DirectorySeparatorChar|], StringSplitOptions.RemoveEmptyEntries)
                           |> Seq.scan (fun st e -> st + Path.DirectorySeparatorChar.ToString() + e) root
                           |> Seq.skip 1
            else yield path
        ]

    let toolboxesFromPaths (matlabPath: string) (pathTofunctionInfo: string -> MatlabFunctionInfo Lazy) (inPaths: string seq) =
        let toolboxPath = Path.Combine(matlabPath, "toolbox")

        let inpaths = inPaths |> Set.ofSeq
        let searchPaths = 
            inPaths |> Seq.toList |> List.sort |> List.collect (decomposePath toolboxPath) 
            |> Seq.filter (fun str -> not <| String.IsNullOrWhiteSpace(str))
            |> Set.ofSeq |> Set.toSeq
        [

            let userIdx = ref 0
            for searchPath in searchPaths do
                let name, helpname = 
                    if searchPath.StartsWith(toolboxPath) then // Actual Matlab Toolbox
                        let helpname = searchPath.Remove(0, toolboxPath.Length + 1)
                        let lidx = searchPath.LastIndexOf(Path.DirectorySeparatorChar)
                        let name = searchPath.Substring(lidx + 1)
                        name, Some helpname
                    else // User Defined "Toolbox"
                        // TODO: Find a better way to name user toolboxes
                        do userIdx := !userIdx + 1
                        "User" + (string !userIdx), None
                let functionInfos = 
                    if inpaths.Contains searchPath then
                        searchPathForFunctionFiles searchPath 
                        |> Seq.map (fun path -> pathTofunctionInfo path)
                    else Seq.empty

                yield { Name = name; Path = searchPath; HelpName = helpname; Funcs = functionInfos; Toolboxes = []}
        ]


    let nestAllToolboxes (tbIn: MatlabToolboxInfo list) = 
        let rec nestToolbox (mtis: MatlabToolboxInfo list) (parent: MatlabToolboxInfo) =
            match mtis with
            | inh :: inresti when inh.Path.StartsWith(parent.Path) -> 
                let inrest, nparent = nestToolbox inresti inh
                nestToolbox inrest { parent with Toolboxes = nparent :: parent.Toolboxes }
            | rest -> rest, parent
        let rec nestToolboxes (mtisIn: MatlabToolboxInfo list) = 
            [
                match mtisIn with
                | h :: resti -> match nestToolbox resti h with
                                | resto, parent -> yield parent; yield! nestToolboxes resto
                | [] -> ()
            ]
        tbIn |> List.sortBy (fun tb -> tb.Path) |> nestToolboxes      

module MatlabCallHelpers = 
    open System
    open System.Text
    open Microsoft.Win32
    
    let processId = lazy (System.Diagnostics.Process.GetCurrentProcess().Id.ToString())

    let getRandomVariableNames =
        seq {
            let procid = processId.Force()
            let randomAddendum = System.IO.Path.GetRandomFileName().Replace('.', '_')
            yield System.String.Format("mtp_{0}_{1}", procid, randomAddendum)
        }

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
        if String.IsNullOrWhiteSpace(whosstr) then [| |]
        else
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

    let commaDelm (strs: string []) = System.String.Join(",", strs)

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
open System.Text

open MatlabCallHelpers

module RepresentationBuilders =     
    let getVariableHandleFromVariableInfo (info: TPVariableInfo) (getContents: string -> obj) (deleteVar: string -> unit) : MatlabVariableHandle =
        let is_disposed = ref false
        {
            Name = info.MatlabVariableInfo.Name
            GetUntyped = fun () -> getContents info.MatlabVariableInfo.Name
            Info = info
            DeleteVariable = fun () -> if not !is_disposed then deleteVar info.MatlabVariableInfo.Name
        }

    let getFunctionHandleFromFunctionInfo (info: MatlabFunctionInfo) (execFuncNamed: obj[] -> string[] -> MatlabVariableHandle[]) (execFuncNum: obj[] -> int -> MatlabVariableHandle[])  : MatlabFunctionHandle =
        {
            Name = info.Name
            Info = info
            Apply = fun args -> 
                        {
                            Name = info.Name
                            ExecuteNamed = fun outVarNames -> execFuncNamed args outVarNames
                            ExecuteNumbered = fun numOutVars -> execFuncNum args numOutVars
                            Info = info
                        }
        }
