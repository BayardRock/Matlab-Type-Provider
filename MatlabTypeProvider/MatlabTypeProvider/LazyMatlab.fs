namespace FSMatlab
open FSMatlab
open FSMatlab.InterfaceTypes

module Data = 
    let GetVariableHandle (name: string) = MatlabInterface.executor.GetVariableHandle(name)
    let GetVariableContents (name: string) = MatlabInterface.executor.GetVariableHandle(name).GetUntyped()
    let SetVariable (name: string) (contents: obj) = MatlabInterface.executor.SetVariable(name, contents, true) 
    let UnsafeOverwriteVariable (name: string) (contents: obj) = MatlabInterface.executor.UnsafeOverwriteVariable(name, contents)

module Exec = 
    let EvaluateExpression (expr: IMatlabExpressionable) = MatlabInterface.executor.ExecuteExpression expr