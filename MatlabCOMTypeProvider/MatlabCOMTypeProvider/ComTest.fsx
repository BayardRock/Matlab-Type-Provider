#r """C:\Users\Rick\Desktop\git\MatlabTypeProvider\MatlabCOMTypeProvider\MatlabCOMTypeProvider\bin\Debug\MatlabCOMTypeProvider.dll"""

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

// Works
proxy.Feval "nthroot" 1 [|2.0; 1.0|]

proxy.Feval "fileparts" 4 [|"""d:\work\consoleApp.cpp"""|]
