#r """..\..\MatlabTypeProvider\MatlabTypeProvider\bin\Debug\MatlabTypeProvider.dll"""

open FSMatlab.Interface

let proxy = new FSMatlab.MatlabCOM.MatlabCOMProxy("Matlab.Desktop.Application")
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

let ml = proxy.MatlabInstance
let mtyp = proxy.MatlabType

//
// GetFull Matrix Playground
//

#r "Microsoft.VisualBasic"

open System
open System.Reflection
open Microsoft.VisualBasic.CompilerServices
open Microsoft.VisualBasic

let getmatrixsize (name: string) =
    let i = exec.GetVariableInfo(name) in i.Value.Size.[0], i.Value.Size.[1] 
    

let getfullmatrix (var: string, xsize: int, ysize: int, hasImag: bool) = 

    let xreal = Array.CreateInstance(typeof<Double>, [|xsize; ysize|])
    let ximag = 
        if hasImag then Array.CreateInstance(typeof<Double>, [|xsize; ysize|])
        else Array2D.zeroCreate 0 0 :> Array

    let argsv : obj []  =  [|var;   "base"; xreal; ximag |]
    let argsc : bool [] =  [|false; false;  true;  true; |]

    LateBinding.LateCall(ml, null, "GetFullMatrix", argsv, null, argsc)

    argsv.[2], argsv.[3]
    

getfullmatrix ("vector", 1, 5, false)
getfullmatrix ("matrix", 3, 3, false)
getfullmatrix ("imag_matrix", 3, 3, true)
getfullmatrix ("imag_vectorT", 1, 3, true)
getfullmatrix ("imag_vector", 3, 1, true)

// WORKS! 
getfullmatrix("imag_single", 1, 1, true) |> fst

proxy.GetFullMatrix("matrix") 3 3

proxy.Execute [| "imag(imag_single)" |]
proxy.Feval "imag" 1 [|"imag_single"|]
proxy.Feval "cos" 1 [|2.0|]

//
// Function call playground
//

let v = [|1.0; 2.0; 3.0|]
proxy.Feval "cos" 1 [|v|]

let v = array2D [|[|1.0; 2.0; 3.0|]; [|4.0;5.0;6.0|]|]
proxy.Feval "cos" 1 [|v|]

let size = let sz = proxy.Feval "size" 1 [|v|] :?> obj [] |> Array.map (fun v -> v :?> double [,]) in (Array.get sz 0) 

proxy.PutFullMatrix("size", size)
proxy.Feval "length" 1 [|size|]
proxy.Feval "sub2ind" 1 [|size, 1, 1|]

//
// Varargin Playground
//
let res = proxy.Feval "strcat" 1 [|"jello"; " world"; " yo"|]
let res = proxy.Feval "strcat" 1 [|"one"; "two"; "three"; "four"|]
MatlabCallHelpers.correctFEvalResult res
let fixFeval (res: obj) = 
        match res with 
        | :? (obj []) as arrayRes -> 
            match arrayRes |> Array.map (MatlabCallHelpers.correctFEvalResult) with
            | arrayRes when arrayRes.Length = 1 -> arrayRes.[0]
            | arrayRes when arrayRes.Length <= 8 ->
                let tupleType = Microsoft.FSharp.Reflection.FSharpType.MakeTupleType(Array.create arrayRes.Length typeof<obj>)
                Microsoft.FSharp.Reflection.FSharpValue.MakeTuple (arrayRes, tupleType)
            | arrayRes -> arrayRes :> obj       
        | unexpected -> failwith (sprintf "Unexpected type returned from Feval: %s" (unexpected.GetType().ToString()))
fixFeval res


(res :?> (obj [])).Length
res.GetType().ToString()
res.GetType().GetElementType().ToString()


let dims = [|2.0; 2.0|]
let indices = [|1.0;2.0;3.0;4.0;5.0;6.0;7.0;8.0|]
let slen = proxy.Feval "length" 1 [|dims|]

//function ndx = sub2ind(siz,varargin)
proxy.Feval "sub2ind" 1 [|[|2.0; 2.0|], indices|]

open System

let correctFEvalResult (res: obj) =
    match res with
    // One array (returned as [,])
    | :? Array as arr when (arr.Rank = 2 && arr.GetLength(0) = 1) -> 
        Array.init (arr.GetLength(1)) (fun idx -> arr.GetValue(0, idx)) :> obj
    | x -> x


// let res = Matlab.Toolboxes.``matlab\elfun``.nthroot([|9.0; 49.0|], 2.0) 
proxy.Feval "nthroot" 1 [|9.0; 2.0|]
let res =  proxy.Feval "nthroot" 1 [|[|9.0; 49.0|]; 2.0|] :?> (obj [])
res.[0].GetType().ToString()

let arr = res.[0] :?> Array
arr.Rank
arr.GetLength(0)


correctFEvalResult res.[0]

//
// Queries
//

let vao = 
    [ for pkg in exec.GetPackageNames() do
        for func in exec.GetPackageFunctions(pkg) do
            yield func ]
    |> Seq.filter (fun f -> f.OutArgs |> List.exists (fun oa -> oa = "varargout"))

vao |> Seq.take 1

//
// Feval with server variables
//

proxy.PutCharArray "name" "The Dude"
proxy.Feval "strcat" 1 [|"My Name is:"; "name="|]


//
// Can we bind the result of an feval?
//
let mutable outvar = "outname=" :> obj
proxy.FevalOut("strcat", 1, [|"My Name is:"; "name="|], &outvar)

//
// How do errors look in Execute?
//
proxy.PutCharArray "x" "hello"
let res = proxy.Execute([|"""nthroot(x,"world")"""|]) :?> string
res.StartsWith("??? Error")

//
// Fevalin out function infos
//
let res = proxy.Feval "nargin" 1 [|"cos"|]
(res :?> obj[]).[0] :?> float |> int
exec.GetFunctionInfoFromName("cos")
exec.GetFunctionNumInputArgs("cos")
exec.GetFunctionNumOutputArgs("cos")

//
// Fancy new nesting
// 

let tbs = exec.GetToolboxes()

open FSMatlab.InterfaceTypes
open InterfaceHelpers.MatlabFunctionHelpers   

let toolboxes = exec.GetToolboxes() |> Seq.toList |> List.sortBy (fun tb -> tb.Path)

toolboxes |> Seq.toList |> nestAllToolboxes 
