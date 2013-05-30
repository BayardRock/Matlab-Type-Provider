namespace FSMatlab.LazyProviderTypes

open FSMatlab.InterfaceTypes

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