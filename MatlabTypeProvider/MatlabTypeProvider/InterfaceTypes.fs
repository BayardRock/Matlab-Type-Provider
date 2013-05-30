module FSMatlab.InterfaceTypes

open System

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
        Path: string
    }

type MatlabVariableInfo = {
        Name: string
        Size: int list
        Bytes: uint64
        Class: string
        Attributes: string list
    }

type MatlabToolboxInfo = {
        Name: string
        Path: string
        HelpName: string option
        Funcs: MatlabFunctionInfo seq
    }

type IMatlabVariableHandle = 
    abstract member Name : string
    abstract member Get : unit -> 'a
    abstract member GetUntyped : unit -> obj
    abstract member Info: MatlabVariableInfo
    abstract member MatlabType : MatlabType
    abstract member LocalType: Type

type IMatlabFunctionHandle = 
    abstract member Name : string
    /// Array can contain a mix of translatable values and variable handles
    abstract member Apply : obj [] -> IMatlabAppliedFunctionHandle
    abstract member Info : MatlabFunctionInfo

and IMatlabAppliedFunctionHandle =
    abstract member Name : string
    /// Takes an array of output variable names and returns handles to outputs
    abstract member Execute : string [] -> IMatlabVariableHandle []
    abstract member Info : MatlabFunctionInfo