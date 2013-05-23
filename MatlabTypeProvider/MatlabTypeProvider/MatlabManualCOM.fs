namespace FSMatlab

module MatlabCOM =
    open System
    open System.Reflection
    open Microsoft.VisualBasic.CompilerServices
    open Microsoft.VisualBasic

    /// Handles all calls to Matlab via the COM interface
    type MatlabCOMProxy (progid: string) =
        do if progid = "" then failwith "Empty matlab progid unexpected"

        let mtyp = Type.GetTypeFromProgID(progid) 
        let ml = Activator.CreateInstance(mtyp)

        /// When "execute" is called the result can actually be the console output, this will remove the default ans variable binding message
        let removeAns (resultStr: string) =
            // Answer string header: [|'\010'; 'a'; 'n'; 's'; ' '; '='; '\010'; '\010';|]
            let ansStr = String([|'\010'; 'a'; 'n'; 's'; ' '; '='; '\010'; '\010'|])
            if resultStr.StartsWith(ansStr) then resultStr.Remove(0, ansStr.Length) else resultStr


        #if DEBUG
        /// Exposed matlab type for testing and experimentation
        member t.MatlabType = mtyp
        /// Exposed matlab instance for testing and experimentation
        member t.MatlabInstance = ml
        #endif

        /// The server returns output from the command in the string, result. The result string also contains any warning or error messages that might have been issued by MATLAB software as a result of the command.
        member t.Execute (args: obj[]) = 
            match mtyp.InvokeMember("Execute", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, args) with
            | :? string as strres -> removeAns strres :> obj
            | other -> other

        /// Feval("functionname", numout, [|arg1;arg2,...|]) 
        /// To reference a variable defined in the server, specify the variable name followed by an equals (=) sign:
        /// a = h.Feval('sin', 1, 'x=');
        member t.Feval (name: string) (numoutparams: int) (args: obj []) : obj = 
            //[| name; noutparams; result, arg1 ... argn |]
            let prms : obj [] = Array.append [| name; numoutparams; null |] args

            // result must be pass by reference
            let mutable prmsMod = ParameterModifier(prms.Length)
            do prmsMod.[2] <- true

            do mtyp.InvokeMember("Feval", Reflection.BindingFlags.InvokeMethod, null, ml, prms, [|prmsMod|], null, null) |> ignore
            prms.[2]
    
        /// Read a char array from matlab as a string
        member t.GetCharArray (var: string) = mtyp.InvokeMember("GetCharArray", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"|] ) :?> string

        /// Read a variable from matlab
        /// If your scripting language requires a result be returned explicitly, use the GetVariable function in place of GetWorkspaceData, GetFullMatrix or GetCharArray.
        /// Do not use GetVariable on sparse arrays, structures, or function handles.
        member t.GetVariable (var: string) = mtyp.InvokeMember("GetVariable", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"|] ) 
    
        /// Get both the real and imaginary parts of a matrix from matlab, not currently working
        member t.GetFullMatrix (var: string, xsize: int, ?ysize: int, ?hasImag: bool) = 
            let ysize =  defaultArg ysize 0 
            let hasImag = defaultArg hasImag false

            let xreal = Array.CreateInstance(typeof<Double>, [|xsize; ysize|])
            let ximag = 
                if hasImag then Array.CreateInstance(typeof<Double>, [|xsize; ysize|])
                else Array.empty<double> :> Array

            let argsv : obj []  =  [|var;   "base"; xreal; ximag |]
            let argsc : bool [] =  [|false; false;  true;  true; |]

            LateBinding.LateCall(ml, null, "GetFullMatrix", argsv, null, argsc)

            argsv.[2] :?> double [,], if hasImag then argsv.[3] :?> double [,] else Array2D.zeroCreate 0 0
    
        /// Use GetWorkspaceData instead of GetFullMatrix and GetCharArray to get numeric and character array data, respectively. Do not use GetWorkspaceData on sparse arrays, structures, or function handles.
        /// These functions use the variant data type instead of the safearray data type used by GetFullMatrix and PutFullMatrix.
        member t.GetWorkspaceData (var: string) = 
            let mutable res : obj = null
            do mtyp.InvokeMember("GetWorkspaceData", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base", res|] ) |> ignore
            res

        //
        // !!! NOTE: Put* Methods have not been tested
        //
        member t.PutCharArray (var:string) (value:string) =  mtyp.InvokeMember("PutCharArray", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; value|] ) 
    
        member t.PutFullMatrix (var: string, xreal: double [,], ?ximag: double [,]) = 
            let ximag : obj = 
                match ximag with
                | Some (arr) -> arr :> obj
                | None -> Array.empty<double> :> obj

            let args : obj [] = [|var; "base"; xreal; ximag|]
            LateBinding.LateCall(ml, null, "PutFullMatrix", args, null, null) 

        /// Use PutWorkspaceData to pass numeric and character array data respectively to the server. Do not use PutWorkspaceData on sparse arrays, structures, or function handles. Use the Execute method for these data types.
        member t.PutWorkspaceData (var: string) (data: obj) = mtyp.InvokeMember("PutWorkspaceData", Reflection.BindingFlags.InvokeMethod ||| Reflection.BindingFlags.Public, null, ml, [|var; "base"; data|] ) 
