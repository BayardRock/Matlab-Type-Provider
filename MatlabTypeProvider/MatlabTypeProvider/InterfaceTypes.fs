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
    /// The name of this variable in Matlab
    abstract member Name : string
    /// Retrieves the contents of this variable from Matlab, statically parameterized by type
    abstract member GetUntyped : unit -> obj
    /// Actual information on the variable as of it's state when this IMatlabVariableHandle was created
    abstract member Info: MatlabVariableInfo
    /// An enumeration representing supported matlab types for conversion to F#
    abstract member MatlabType : MatlabType
    /// The .net type that this variable will be converted to when gotten
    abstract member LocalType: Type
    /// Removes this variable from matlab scope.  After this all calls which go to matlab will fail with an exception. 
    abstract member Delete : unit -> unit


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
