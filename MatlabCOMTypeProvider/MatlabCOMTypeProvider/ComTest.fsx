#r """..\MatlabCOMTypeProvider\bin\Debug\MatlabCOMTypeProvider.dll"""

open FSMatlab.COMInterface

let proxy = new MatlabCOM.MatlabCOMProxy("Matlab.Desktop.Application")
let exec = new MatlabCommandExecutor(proxy)

//let v = proxy.GetVariable("pkg")
let v2 : obj = proxy.GetWorkspaceData("pkg")

let funcs = exec.GetPackageFunctions("NET")

proxy.Execute [|"strjoin(cellfun(@(x) x.Name, meta.package.getAllPackages(), 'UniformOutput', false)', ';')"|] :?> string |> (fun c -> c.ToCharArray())
proxy.Execute [|"disp(path)"|]


let mlpath = exec.GetRoot()
let tbpaths = exec.GetFunctionSearchPaths()

let toolboxes = toolboxesFromPaths mlpath tbpaths |> Seq.toArray

open System.IO

Path.Combine(mlpath.Trim(), "toolbox\\")
Path.GetDirectoryName(mlpath)
mlpath.Trim()

// FEval Tests -- Works
proxy.Feval "nthroot" 1 [|2.0; 1.0|]
proxy.Feval "fileparts" 4 [|"""d:\work\consoleApp.cpp"""|]


// GetFullMatrix Test --
proxy.Execute([|"matrix = [1.0 2.0 3.0;4.0 5.0 6.0;7.0 8.0 9.0]" |])
proxy.Execute([|"vector = [1 2 3 4 5]" |])
proxy.Execute([|"imag_single = rand(1,1)+i*rand(1,1)" |])
proxy.Execute([|"imag_vector = rand(1,3)+i*rand(1,3)" |])
proxy.Execute([|"imag_matrix = rand(3,3)+i*rand(3,3)" |])
open System
open System.Reflection

let ml = proxy.MatlabInstance
let mtyp = proxy.MatlabType

#r "Microsoft.VisualBasic"

open Microsoft.VisualBasic.CompilerServices
open Microsoft.VisualBasic

let getmatrixsize (name: string) =
    let i = exec.GetVariableInfo(name) in i.Value.Size.[0], i.Value.Size.[1] 
    

let getfullmatrix var (xsize: int) (ysize:int) = 
        let memptyw = Array.empty<double>
       
        let xreal = Array.CreateInstance(typeof<System.Numerics.Complex>, [|xsize; ysize|])
        let ximag = Array.CreateInstance(typeof<System.Numerics.Complex>, [|xsize; ysize|])

        let argsv : obj []  =  [|var;   "base"; xreal; ximag |]
        let argsc : bool [] =  [|false; false;  true;  true; |]


        LateBinding.LateCall(ml, null, "GetFullMatrix", argsv, null, argsc)

        argsv.[2], argsv.[3]

getfullmatrix "vector" 1 5
getfullmatrix "matrix" 3 3 
getfullmatrix "imag_matrix" 3 3
getfullmatrix "imag_single" 1 1

proxy.GetFullMatrix("matrix") 3 3

proxy.Execute [| "imag(imag_single)" |]
proxy.Feval "imag" 1 [|"imag_single"|]
proxy.Feval "cos" 1 [|2.0|]
