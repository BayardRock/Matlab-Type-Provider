#r """C:\Users\Rick\Desktop\git\MatlabTypeProvider\MatlabCOMTypeProvider\MatlabCOMTypeProvider\bin\Debug\MatlabCOMTypeProvider.dll"""

open FSMatlab.COMInterface

let proxy = new MatlabCOMProxy("Matlab.Desktop.Application")
let exec = new MatlabCommandExecutor(proxy)

//let v = proxy.GetVariable("pkg")
let v2 : obj = proxy.GetWorkspaceData("pkg")

let funcs = exec.GetPackageFunctions("NET")

proxy.Execute [|"strjoin(cellfun(@(x) x.Name, meta.package.getAllPackages(), 'UniformOutput', false)', ';')"|] :?> string |> (fun c -> c.ToCharArray())
