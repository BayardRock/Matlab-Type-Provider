﻿module InterfaceHelpers

open FSMatlab.InterfaceTypes

module TypeConverters =
    let private getMatlabTypeFromMatlabSig =
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

    let private getDotNetType = 
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

    let constructTypeProviderVariableInfo (mvi: MatlabVariableInfo) =
        let mltype = getMatlabTypeFromMatlabSig mvi
        let dntype = getDotNetType mltype
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
        //let genEmptyFunc () = { Name = Path.GetFileNameWithoutExtension(fullPath) ; InParams = [ "varargin" ] ; OutParams = [ "varargout" ] ; Path = fullPath }
        try 
            match StringWindow(File.ReadAllText(fullPath), 0u) |> findFunc |> Option.map (parseFunDecl) with
            | Some (name, inparams, outparams) -> Some { Name = name; InParams = inparams; OutParams = outparams; Path = Some fullPath }
            | None -> None //genEmptyFunc () 
        with ex -> None //genEmptyFunc ()

    let searchPathForFunctionFiles (searchPath: string) : string seq =
        seq {                                         
            for file in Directory.EnumerateFiles(searchPath, "*.m") do
                yield Path.Combine(searchPath, file)
                //match pathToFunctionInfo fullPath with 
                //| Some (fi) -> yield fi 
                //| None -> ()
        }        

    let toolboxesFromPaths (matlabPath: string) (searchPaths: string seq) (functionInfoBuilder: string -> MatlabFunctionInfo) = 
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
                let functionInfos = 
                    searchPathForFunctionFiles searchPath 
                    |> Seq.map (fun path -> functionInfoBuilder path)

                yield { Name = name; Path = searchPath; HelpName = helpname; Funcs = functionInfos }
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




open System
open System.Reflection
open System.Numerics
open System.Text

open MatlabCallHelpers

module RepresentationBuilders =     
    let getVariableHandleFromVariableInfo (info: TPVariableInfo) (getContents: string -> MatlabType -> obj) (deleteVar: string -> unit) : IMatlabVariableHandle =
        let is_disposed = ref false
        { new IMatlabVariableHandle with
                member t.Name = info.MatlabVariableInfo.Name 
                member t.GetUntyped () = getContents info.MatlabVariableInfo.Name  info.MatlabType 
                member t.Info = info
                member t.Dispose () = if not !is_disposed then deleteVar info.MatlabVariableInfo.Name
            interface IDisposable with
                member t.Dispose () = if not !is_disposed then deleteVar info.MatlabVariableInfo.Name
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
