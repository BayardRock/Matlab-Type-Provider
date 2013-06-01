module Matlab.TypeProvider.LazyFunctions.Tests

open System
open Xunit


open LazyMatlab
open FSMatlab.InterfaceTypes
open LazyMatlab.InterfaceTypeExtensionsAndHelpers

[<Fact>]
let ``simple function calls should execute and return a single correct answer`` () =
    let appliedHandle = Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) 
    let resultVarName = "test1_output"
    let [| res_var |] = appliedHandle.Execute([|resultVarName|]) // You must name the output on the matlab side
    let res_untyped = res_var.GetUntyped() :?> double
    let res_typed : double = res_var.Get()
    Assert.Equal(3.0, res_untyped)
    Assert.Equal(3.0, res_typed)
    Assert.Equal<string>(resultVarName, res_var.Name)
    res_var.Delete()

[<Fact>]
let ``simple function calls should execute and return a single correct answer with helpers`` () =
    let (E1(res_var)) = Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) 
    let res_untyped = res_var.GetUntyped() :?> double
    let res_typed : double = res_var.Get()
    Assert.Equal(3.0, res_untyped)
    Assert.Equal(3.0, res_typed)
    res_var.Delete()

[<Fact>]
let ``simple function calls should execute and return a single correct answer with execute and retrieve helper`` () =
    let (ER1(res_untyped)) = Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) 
    Assert.Equal(3.0, res_untyped :?> double)
