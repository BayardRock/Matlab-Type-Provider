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
        Toolboxes: MatlabToolboxInfo list
    }

type IMatlabExpressionable =
    abstract member ToExpression: unit -> MatlabExpression

and MatlabExpression = 
     | Var of string
     | InfixOp of string * MatlabExpression * MatlabExpression
     with
        interface IMatlabExpressionable with member t.ToExpression() = t
        static member (+) (a: MatlabExpression, b: IMatlabExpressionable) = InfixOp("+", a, b.ToExpression())
        static member (-) (a: MatlabExpression, b: IMatlabExpressionable) = InfixOp("-", a, b.ToExpression())
        static member (*) (a: MatlabExpression, b: IMatlabExpressionable) = InfixOp("*", a, b.ToExpression())
        static member (/) (a: MatlabExpression, b: IMatlabExpressionable) = InfixOp("/", a, b.ToExpression())
        static member (.*) (a: MatlabExpression, b: IMatlabExpressionable) = InfixOp(".*", a, b.ToExpression())
        static member (./) (a: MatlabExpression, b: IMatlabExpressionable) = InfixOp("./", a, b.ToExpression())
        static member (.^) (a: MatlabExpression, b: IMatlabExpressionable) = InfixOp(".^", a, b.ToExpression())


and MatlabVariableHandle =
    {
        /// Deletes this variable inside of Matlab.  Will be called on dispose. 
        DeleteVariable: unit -> unit
        /// The name of this variable in Matlab
        Name: string
        /// Retrieves the contents of this variable from Matlab, statically parameterized by type
        GetUntyped : unit -> obj
        /// Actual information on the variable as of it's state when this IMatlabVariableHandle was created
        Info: TPVariableInfo
    }
    member t.Get () : 'a =  t.GetUntyped() :?> 'a
    interface IDisposable with member t.Dispose() = t.DeleteVariable()
    interface IMatlabExpressionable with member t.ToExpression() = Var(t.Name)
    static member (+) (a: MatlabVariableHandle, b: IMatlabExpressionable) = InfixOp("+", Var(a.Name), b.ToExpression())
    static member (-) (a: MatlabVariableHandle, b: IMatlabExpressionable) = InfixOp("-", Var(a.Name), b.ToExpression())
    static member (*) (a: MatlabVariableHandle, b: IMatlabExpressionable) = InfixOp("*", Var(a.Name), b.ToExpression())
    static member (/) (a: MatlabVariableHandle, b: IMatlabExpressionable) = InfixOp("/", Var(a.Name), b.ToExpression())
    static member (.*) (a: MatlabVariableHandle, b: IMatlabExpressionable) = InfixOp(".*", Var(a.Name), b.ToExpression())
    static member (./) (a: MatlabVariableHandle, b: IMatlabExpressionable) = InfixOp("./", Var(a.Name), b.ToExpression())
    static member (.^) (a: MatlabVariableHandle, b: IMatlabExpressionable) = InfixOp(".^", Var(a.Name), b.ToExpression())

/// Represents a Matlab Function
and MatlabFunctionHandle = 
    {
        /// The name of the matlab function as called
        Name: string
        /// Array can contain a mix of translatable values and variable handles
        Apply: obj [] -> MatlabAppliedFunctionHandle
        /// Reflected Matlab Function Information 
        Info: MatlabFunctionInfo
    }

/// Represents a Matlab Function with arguments applied
and MatlabAppliedFunctionHandle = 
    {
        /// The name of the matlab function as called
        Name: string
        /// Executes the function specifying the given output variable names and returns handles to these outputs
        ExecuteNamed: string [] -> MatlabVariableHandle []
        /// Executes the function specifying N outputs which will be randomly named and returned
        ExecuteNumbered: int -> MatlabVariableHandle []
        /// Reflected Matlab Function Information 
        Info : MatlabFunctionInfo
    }
