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

type MTPFunctionHandle = 
    {
        Name: string
        Varargin: bool
        Varargout: bool
        InArgs: obj []
    }

type MTPVariableHandle = 
    {
        Name: string
        Info: MatlabVariableInfo
    }