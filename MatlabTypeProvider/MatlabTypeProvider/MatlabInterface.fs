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

    member t.Execute(command: string) =
        let varName = t.GenerateSafeVariableName()
        do proxy.Execute([|varName + " = " + command + ";"|]) |> ignore
        t.GetVariableHandle(varName)

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

        MatlabFunctionHelpers.toolboxesFromPaths matlabRoot functionBuilder searchPaths

    member t.GetFunctionHandle (funcInfo: MatlabFunctionInfo) =
        let fexecNamed (inargs: obj []) (outargs: string []) = t.CallFunctionWithHandles(funcInfo.Name, outargs, inargs)
        let fexecNum (inargs: obj []) (out: int) = t.CallFunctionWithHandles(funcInfo.Name, out, inargs)
        RepresentationBuilders.getFunctionHandleFromFunctionInfo funcInfo fexecNamed fexecNum


    member t.GetMatlabVariableInfos() =  proxy.Execute [|"whos"|] :?> string |> parseWhos
    member t.GetVariableInfos() : TPVariableInfo [] = proxy.Execute [|"whos"|] :?> string |> parseWhos |> Array.map TypeConverters.constructTypeProviderVariableInfo
    member t.GetVariableInfo name : TPVariableInfo option = proxy.Execute [|"whos " + name|] :?> string |> parseWhos |> Array.map TypeConverters.constructTypeProviderVariableInfo |> Array.tryFind (fun _ -> true)

    member t.UnsafeOverwriteVariable(name: string, value: obj) : MatlabVariableHandle =
        t.SetVariable(name, value, overwrite = true).Value

    member private t.SetSimpleVariable(name: string, value: obj) : MatlabVariableHandle =
        proxy.PutWorkspaceData name value
        t.GetVariableHandle(name)   

    member private t.SetMatrix<'T>(name: string, m: 'T [,]) : MatlabVariableHandle =
        match m :> obj with
        | :? (Complex [,]) as comp -> 
            let real = Array2D.init (comp.GetLength(0)) (comp.GetLength(1)) (fun i j -> comp.[i,j].Real)
            let imag = Array2D.init (comp.GetLength(0)) (comp.GetLength(1)) (fun i j -> comp.[i,j].Imaginary)
            do proxy.PutFullMatrix(name, real, imag)
        | _ -> proxy.PutWorkspaceData name m
        t.GetVariableHandle(name)

    member private t.SetLargeMatrix<'T>(name: string, m: 'T [,]) : MatlabVariableHandle =
        let max_doubles = 8388608 / (sizeof<'T>) // 8mb at a time
        let m_height = m.GetLength(0)
        let m_width = m.GetLength(1)
        let m_rows_per_iter = max_doubles / m_width
        let iters = m_height / m_rows_per_iter // int division, rounds down
        if iters = 0 then t.SetMatrix(name, m)
        else
            let tmpName = t.GenerateSafeVariableName () 
            let varName = t.GenerateSafeVariableName () 
            proxy.Execute([|varName + " = [];"|]) |> ignore
            let vertcatStr = varName + " = vertcat(" + varName + "," + tmpName + ");"
            for i = 0 to iters - 1 do
                let sidx = m_rows_per_iter * i
                let eidx = m_rows_per_iter * (i + 1) - 1
                let slice = m.[sidx..eidx,*]
                let th = t.SetMatrix(tmpName, slice)
                try proxy.Execute([|vertcatStr|]) |> ignore
                finally th.DeleteVariable()
            let sidx = iters * m_rows_per_iter
            let slice = m.[sidx..,*]
            let th = t.SetMatrix(tmpName, slice)
            try proxy.Execute([|vertcatStr|]) |> ignore
            finally th.DeleteVariable()
            t.GetVariableHandle(varName)


    member t.SetVariable(name: string, value: obj, ?overwrite: bool) : MatlabVariableHandle option =
        let overwrite = defaultArg overwrite false
        let vtype = if value = null then failwith (sprintf "Cannot set %s to null" name) else value.GetType()
        let var_doesnt_exist = t.GetVariableInfo(name).IsNone
        if overwrite || var_doesnt_exist then
            match value with
            | :? (double [,]) as value -> t.SetLargeMatrix(name, value) |> Some
            | :? (Complex [,]) as value -> t.SetLargeMatrix(name, value) |> Some
            | _ -> t.SetSimpleVariable(name, value) |> Some
        else None

    member t.DeleteVariable(name: string) : unit = 
        proxy.Execute([|"clear " + name|]) |> ignore

    member t.GetVariableHandle(info: TPVariableInfo) =
        let getFunc name = t.GetVariableContents(name)
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

    member t.ExecuteString (formattedCall: string) =
        let res = proxy.Execute([|formattedCall|]) :?> string 
        // Fail if things didn't work out
        if res.Trim().StartsWith("??? Error") then raise <| MatlabErrorException (sprintf "Formatted call (%s) gave the following error (%s)" formattedCall res) 
        res 

    member t.GenerateSafeVariableName () : string =
        getRandomVariableNames |> Seq.find (fun vn -> (t.GetVariableInfo vn).IsNone)

    /// Call a function with either MatlabVariableHandles or Objs, Returns a handle
    /// Note: Will create temporary variables with random names on the matlab side
    member t.CallFunctionWithHandles (name: string, outArgNames: string [], args: obj []) : MatlabVariableHandle [] = 
        // Push non-handle variables to matlab, and keep track of which were pushed just for this function
        let inArgs =
            [| for arg in args do 
                match arg with 
                | :? MatlabVariableHandle as h -> yield h, false
                | o -> 
                    let varname = t.GenerateSafeVariableName()
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
            try t.ExecuteString (formattedCall)
            finally  
                // Delete inargs variables that were generated just for this call
                for arg, deleteMe in inArgs do
                    if deleteMe then try arg.DeleteVariable() with _ -> ()

        // Build result handles
        [| for outArgName in outArgNames do yield t.GetVariableHandle(outArgName) |]

    /// Just like the other CallFunctionWithHandles but will use randomized result value names 
    member t.CallFunctionWithHandles (name: string, numout: int, args: obj []) : MatlabVariableHandle [] = 
        let currentVars = lazy (t.GetVariableInfos() |> Array.map (fun vi -> vi.Name))
        let outargs = Array.init numout (fun _ -> t.GenerateSafeVariableName())
        t.CallFunctionWithHandles(name, outargs, args)

//    member private t.GetSmallMatrix { Name = vname; Size = [sx;sy] } = 
//        proxy.GetFullMatrix(vname,sx,sy) |> fst

    member private t.GetLargeMatrix<'T> (mvi: MatlabVariableInfo) =
        let max_bytes = 8388608 // ~8 mb at a time
        let m_height = mvi.Size.[0]
        let m_width = mvi.Size.[1]
        let output_matrix = Array2D.zeroCreate m_height m_width
        let elm_size = (float mvi.Bytes) / ((float m_height) * (float m_width)) 
        let max_doubles = (float max_bytes) / elm_size |> int
        let m_rows_per_iter = max_doubles / m_width
        let iters = m_height / m_rows_per_iter // int division, rounds down
        for i = 0 to iters - 1 do
            let sidx = m_rows_per_iter * i
            let eidx = m_rows_per_iter * (i + 1) - 1 
            let cmd = String.Format("{0}({1}:{2},:)", mvi.Name, (sidx + 1), (eidx + 1))
            use iter_handle = t.Execute(cmd)
            let data = iter_handle.Get<'T [,]>()
            do Array2D.blit data 0 0 output_matrix sidx 0 (data.GetLength(0)) (data.GetLength(1))
        let sidx = iters * m_rows_per_iter
        use iter_handle = t.Execute(String.Format("{0}({1}:end, :)", mvi.Name, sidx + 1))
        let data  = iter_handle.Get<'T [,]>()
        do Array2D.blit data 0 0 output_matrix sidx 0 (data.GetLength(0)) (data.GetLength(1))
        output_matrix        

    member t.GetVariableContents (vname: string) = 
        let max_bytes = 8388608
        let vi = 
            match t.GetVariableInfo(vname) with
            | Some (vi) -> vi
            | None -> failwith (sprintf "Variable %s does not exist" vname)           
        let mvi = vi.MatlabVariableInfo
        let vtype = vi.MatlabType

        match vtype, mvi with
        | MatlabType.MString, _ -> proxy.GetCharArray(vname) :> obj
        | MatlabType.MDouble, _ -> proxy.GetVariable(vname) 
        | MatlabType.MVector, { Size = [_; sy] } -> 
            let real, _ = proxy.GetFullMatrix(vname, 1, sy, hasImag = false) 
            let carr = Array.CreateInstance(typeof<double>, sy) :?> double []
            for i = 0 to sy - 1 do Array.set carr i (real.[0,i])
            carr :> obj
        | MatlabType.MMatrix, { Bytes = b; Size = [sx;sy] } -> 
            if b <= uint64 max_bytes then proxy.GetFullMatrix(vname,sx,sy) |> fst :> obj
            else t.GetLargeMatrix<float> mvi |> box
        | MatlabType.MComplexDouble, _ -> 
            let r, i = proxy.GetFullMatrix(vname,1,1,true) |> fun (r,i) -> r.[0,0], i.[0,0]
            Complex(r, i) :> obj
        | MatlabType.MComplexVector, { Size = [1; sy]; Attributes = ["complex"] } ->
            let real, imag = proxy.GetFullMatrix(vname, 1, sy, hasImag = true) 
            let carr = Array.CreateInstance(typeof<System.Numerics.Complex>, sy) :?> Complex [] 
            for i = 0 to sy - 1 do carr.[i] <- (Complex(real.[0,i], imag.[0,i]))
            carr :> obj
        | MatlabType.MComplexMatrix, { Bytes = b; Size = [sx;sy]; Attributes = ["complex"] } -> 
            if b <= uint64 max_bytes then 
                let real, imag = proxy.GetFullMatrix(vname, sx, ysize = sy, hasImag = true) 
                let carr = Array.CreateInstance(typeof<System.Numerics.Complex>, sx, sy) :?> Complex [,] 
                for i = 0 to sx - 1 do
                    for j = 0 to sy - 1 do
                        carr.[i,j] <- (Complex(real.[i,j], imag.[i,j]))
                carr :> obj
            else t.GetLargeMatrix<Complex>(mvi) |> box
        | MatlabType.MLogical, _ -> proxy.GetVariable(vname)
        | MatlabType.MLogicalVector, { Size = [1; sy] } -> 
            let v = proxy.GetVariable(vname) :?> bool [,]
            v |> Seq.cast<bool> |> Seq.toArray :> obj
        | MatlabType.MLogicalMatrix, { Bytes = b; Size = [sx;sy] } -> 
            if b <= uint64 max_bytes then proxy.GetVariable(vname)
            else t.GetLargeMatrix<bool>(mvi) |> box
        | MatlabType.MUnexpected, _ -> proxy.GetVariable(vname)
        | _ -> failwith (sprintf "Could not read Unexpected/Unsupported Variable Type: %A" vtype)

    member t.ExecuteExpression (expr: IMatlabExpressionable) =
        let rec eval (expr: MatlabExpression) = 
            match expr with 
            | Var (v) -> v
            | InfixOp (op, l, r) -> String.Format("({0} {1} {2})", (eval l), op, (eval r))
        let execstr = eval (expr.ToExpression()) 
        let outvarname = t.GenerateSafeVariableName()
        let res = t.ExecuteString(String.Format("{0} = {1}", outvarname, execstr))
        use vhandle = t.GetVariableHandle outvarname
        vhandle.GetUntyped()