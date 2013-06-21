namespace LazyMatlab
open FSMatlab

module Data = 
    let GetVariableHandle (name: string) = MatlabInterface.executor.GetVariableHandle(name)
    let GetVariableContents (name: string) = MatlabInterface.executor.GetVariableHandle(name).GetUntyped()
    let SetVariable (name: string) (contents: obj) = MatlabInterface.executor.SetVariable(name, contents, true) 
