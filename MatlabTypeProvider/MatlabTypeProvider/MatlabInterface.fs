module FSMatlab.Interface

open System
open System.Text
open System.Numerics

open FSMatlab.MatlabCOM
open InterfaceTypes
open InterfaceHelpers
open InterfaceHelpers.MatlabCallHelpers

type MatlabCommandExecutor(proxy: MatlabCOMProxy) =

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
        |> function 
           | Some (help) -> help 
           | None -> 
                match f.Path with 
                | Some path -> "Function Path: " + path + Environment.NewLine + "No help available for this function"
                | None -> "No help available for this function"

    member t.GetPackageNames() =
         proxy.Execute [|"strjoin(cellfun(@(x) x.Name, meta.package.getAllPackages(), 'UniformOutput', false)', ';')"|] 
         :?> string |> parsePackages |> filterPackages

    member t.GetPackageMethods (pkgName: string) = 
        proxy.Execute [|MatlabStrings.getPackageFunctions pkgName|] :?> string |> tsvToMethodInfo

    member t.GetFunctionInfoFromFile (path: string) = MatlabFunctionHelpers.fileToFunctionInfo path   

    member t.GetFunctionInfoFromName (funcname: string) = 
        try 
            let numArgsIn, varArgsIn = t.GetFunctionNumInputArgs(funcname)
            let numArgsOut, varArgsOut = t.GetFunctionNumOutputArgs(funcname)
            let inparams = [ for i = 0 to numArgsIn - 2 do 
                                yield "arg" + (string i)
                             yield if varArgsIn then "varargin" else "arg" + (string (numArgsIn - 1)) ] 
            let outparams = [ for i = 0 to numArgsOut - 2 do 
                                yield "oarg" + (string i)
                              yield if varArgsOut then "varargout" else "oarg" + (string (numArgsOut - 1)) ] 
            { Name = funcname; InParams = inparams; OutParams = outparams; Path = None }
        with _ -> { Name = funcname; InParams = ["varargin"]; OutParams = ["varargout"]; Path = None }        

    member t.GetToolboxes () = 
        let matlabRoot = proxy.Execute [|"matlabroot"|] :?> string |> (fun str -> str.Trim())
        let searchPaths = proxy.Execute [|"disp(path)"|] :?> string |> (fun paths -> paths.Split([|';'|], StringSplitOptions.RemoveEmptyEntries)) |> Array.map (fun str -> str.Trim())
        let functionBuilder path = 
            match t.GetFunctionInfoFromFile(path) with
            | Some fi -> fi 
            | None -> t.GetFunctionInfoFromName(System.IO.Path.GetFileNameWithoutExtension(path))

        MatlabFunctionHelpers.toolboxesFromPaths matlabRoot searchPaths functionBuilder

    member t.GetFunctionHandle (funcInfo: MatlabFunctionInfo) =
        let fexecNamed (inargs: obj []) (outargs: string []) = t.CallFunctionWithHandles(funcInfo.Name, outargs, inargs)
        let fexecNum (inargs: obj []) (out: int) = t.CallFunctionWithHandles(funcInfo.Name, out, inargs)
        RepresentationBuilders.getFunctionHandleFromFunctionInfo funcInfo fexecNamed fexecNum


    member t.GetMatlabVariableInfos() =  proxy.Execute [|"whos"|] :?> string |> parseWhos
    member t.GetVariableInfos() : TPVariableInfo [] = proxy.Execute [|"whos"|] :?> string |> parseWhos |> Array.map TypeConverters.constructTypeProviderVariableInfo
    member t.GetVariableInfo name : TPVariableInfo option = proxy.Execute [|"whos " + name|] :?> string |> parseWhos |> Array.map TypeConverters.constructTypeProviderVariableInfo |> Array.tryFind (fun _ -> true)

    member t.OverwriteVariable(name: string, value: obj, ?overwrite: bool) : IMatlabVariableHandle =
        proxy.PutWorkspaceData name value
        t.GetVariableHandle(name)
                    
    member t.SetVariable(name: string, value: obj, ?overwrite: bool) : IMatlabVariableHandle option =
        // TODO: Proper Conversions
        let overwrite = defaultArg overwrite false
        let vtype = if value = null then failwith (sprintf "Cannot set %s to null" name) else value.GetType()
        // Quick check to see if the type is convertable (some types may actually work but be blocked by this)
        do TypeConverters.getMatlabTypeFromDotNetSig vtype |> ignore
        let var_doesnt_exist = t.GetVariableInfo(name).IsNone
        if overwrite || var_doesnt_exist then
            proxy.PutWorkspaceData name value
            t.GetVariableHandle(name) |> Some
        else None

    member t.DeleteVariable(name: string) : unit = 
        proxy.Execute([|"clear " + name|]) |> ignore

    member t.GetVariableHandle(info: TPVariableInfo) =
        let getFunc name mltype = t.GetVariableContents(name, mltype)
        let deleteFunc name = t.DeleteVariable(name)
        RepresentationBuilders.getVariableHandleFromVariableInfo info getFunc deleteFunc       

    member t.GetVariableHandle(name: string) = 
        let argInfo = match t.GetVariableInfo(name) with
                      | Some (name) -> name
                      | None -> failwith (sprintf "Variable not found: %s" name)
        t.GetVariableHandle(argInfo)

    /// Returns number of parameters and if the last parameter is varargs
    member t.GetFunctionNumInputArgs(name: string) : (int * bool) = 
        let ret = proxy.Feval "nargin" 1 [| name |] |> (fun (a:obj) -> (a :?> obj[]).[0] :?> float) |> int
        abs(ret), ret < 0

    /// Returns number of parameters and if the last parameter is varargs
    member t.GetFunctionNumOutputArgs(name: string) : (int * bool) = 
        let ret = proxy.Feval "nargout" 1 [| name |] |> (fun (a:obj) -> (a :?> obj[]).[0] :?> float) |> int
        abs(ret), ret < 0

    /// Call a function with either MatlabVariableHandles or Objs, Returns a handle
    /// Note: Will create temporary variables with random names on the matlab side
    member t.CallFunctionWithHandles (name: string, outArgNames: string [], args: obj []) : IMatlabVariableHandle [] = 
        // Push non-handle variables to matlab, and keep track of which were pushed just for this function
        let currentVars = lazy (t.GetMatlabVariableInfos() |> Array.map (fun vi -> vi.Name))
        let inArgs =
            [| for arg in args do 
                match arg with 
                | :? IMatlabVariableHandle as h -> yield h, false
                | o -> 
                    let varname = getSafeRandomVariableName (currentVars.Force()) 
                    // Side Effect: Sets variables matlab side, be sure to delete them after
                    match t.SetVariable(varname, o) with
                    | Some (handle) -> yield handle, true 
                    | None -> failwith (sprintf "Variable with this name already exists: %s" varname)
            |]   

        // Generate call text
              
        let formattedOutArgs = commaDelm outArgNames
        let formattedInArgs = commaDelm (inArgs |> Array.map (fun (f,s) -> f.Name))
        let formattedCall = String.Format("[{0}] = {1}({2})", formattedOutArgs, name, formattedInArgs)

        // Make call
        let res = 
            try 
                let res = proxy.Execute([|formattedCall|]) :?> string 
                // Fail if things didn't work out
                if res.Trim().StartsWith("??? Error") then raise <| MatlabErrorException (sprintf "Formatted call (%s) gave the following error (%s)" formattedCall res) 
                res
            finally  
                // Delete inargs variables that were generated just for this call
                for arg, deleteMe in inArgs do
                    if deleteMe then try arg.Dispose() with _ -> ()

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
        let vi = 
            match t.GetVariableInfo(vname) with
            | Some (vi) -> vi
            | None -> failwith (sprintf "Variable %s does not exist" vname)           
        let mvi = vi.MatlabVariableInfo

        match vtype, mvi with
        | MatlabType.MString, _ -> proxy.GetCharArray(vname) :> obj
        | MatlabType.MDouble, _ -> proxy.GetVariable(vname) 
        | MatlabType.MVector, { Size = [_; sy] } -> 
            let real, _ = proxy.GetFullMatrix(vname, 1, sy, hasImag = false) 
            let carr = Array.CreateInstance(typeof<double>, sy) :?> double []
            for i = 0 to sy - 1 do Array.set carr i (real.[0,i])
            carr :> obj
        | MatlabType.MMatrix, { Size = [sx;sy] } -> proxy.GetFullMatrix(vname,sx,sy) |> fst :> obj
        | MatlabType.MComplexDouble, _ -> 
            let r, i = proxy.GetFullMatrix(vname,1,1,true) |> fun (r,i) -> r.[0,0], i.[0,0]
            Complex(r, i) :> obj
        | MatlabType.MComplexVector, { Size = [1; sy]; Attributes = ["complex"] } ->
            let real, imag = proxy.GetFullMatrix(vname, 1, sy, hasImag = true) 
            let carr = Array.CreateInstance(typeof<System.Numerics.Complex>, sy) :?> Complex [] 
            for i = 0 to sy - 1 do carr.[i] <- (Complex(real.[0,i], imag.[0,i]))
            carr :> obj
        | MatlabType.MComplexMatrix, { Size = [sx;sy]; Attributes = ["complex"] } -> 
            let real, imag = proxy.GetFullMatrix(vname, sx, ysize = sy, hasImag = true) 
            let carr = Array.CreateInstance(typeof<System.Numerics.Complex>, sx, sy) :?> Complex [,] 
            for i = 0 to sx - 1 do
                for j = 0 to sy - 1 do
                    carr.[i,j] <- (Complex(real.[i,j], imag.[i,j]))
            carr :> obj   
        | _ -> failwith (sprintf "Could not read Unexpected/Unsupported Variable Type: %A" vtype)


