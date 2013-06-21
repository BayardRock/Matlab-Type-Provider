﻿module TestHelpers

open Xunit

let AssertNoVariableChanges (testfun: unit -> unit) =   
    let startVars = FSMatlab.MatlabInterface.executor.GetVariableInfos()
    testfun()
    let endVars = FSMatlab.MatlabInterface.executor.GetVariableInfos()
    Assert.Equal<FSMatlab.InterfaceTypes.TPVariableInfo []>(startVars, endVars)

let AssertVariableIsDeleted (name: string) =
    Assert.True(FSMatlab.MatlabInterface.executor.GetVariableInfo(name) = None)