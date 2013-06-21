module FSMatlab.InterfaceTypes

open System

exception MatlabErrorException of string

type MatlabType = 
    | MString = 0
    | MDouble = 1
    | MVector = 2
    | MMatrix = 3
    | MComplexDouble = 4
    | MComplexVector = 5
    | MComplexMatrix = 6
    | MUnexpected = 7

type MatlabMethodInfo = {
        Name: string
        InArgs: string list
        OutArgs: string list
        Access: string
        Static: bool
    }

type MatlabFunctionInfo = {
        Name: string
        InParams: string list
        OutParams: string list
        Path: string option
    }

type MatlabVariableInfo = {
        Name: string
        Size: int list
        Bytes: uint64
        Class: string
        Attributes: string list
    }

type TPVariableInfo = {
        MatlabVariableInfo: MatlabVariableInfo
        MatlabType: MatlabType
        Type: System.Type
    }
    with 
        member t.Name = t.MatlabVariableInfo.Name
        member t.Size = t.MatlabVariableInfo.Size

type MatlabToolboxInfo = {
        Name: string
        Path: string
        HelpName: string option
        Funcs: MatlabFunctionInfo seq
    }

type IMatlabVariableHandle = 
    inherit IDisposable
    abstract member Dispose: unit -> unit
    /// The name of this variable in Matlab
    abstract member Name : string
    /// Retrieves the contents of this variable from Matlab, statically parameterized by type
    abstract member GetUntyped : unit -> obj
    /// Actual information on the variable as of it's state when this IMatlabVariableHandle was created
    abstract member Info: TPVariableInfo

type IMatlabFunctionHandle = 
    abstract member Name : string
    /// Array can contain a mix of translatable values and variable handles
    abstract member Apply : obj [] -> IMatlabAppliedFunctionHandle
    abstract member Info : MatlabFunctionInfo

and IMatlabAppliedFunctionHandle =
    abstract member Name : string
    /// Executes the function specifying the given output variable names and returns handles to these outputs
    abstract member Execute : string [] -> IMatlabVariableHandle []
    /// Executes the function specifying N outputs which will be randomly named and returned
    abstract member Execute : int -> IMatlabVariableHandle []
    abstract member Info : MatlabFunctionInfo

type MatlabExpression = 
    | Var of IMatlabVariableHandle
    // The string represents the op to be used
    | InfixOp of string * MatlabExpression * MatlabExpression
    | Function of IMatlabFunctionHandle * MatlabExpression []