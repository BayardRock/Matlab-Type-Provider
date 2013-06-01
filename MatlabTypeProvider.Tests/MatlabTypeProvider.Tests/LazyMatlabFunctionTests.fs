module Matlab.TypeProvider.LazyFunctions.Tests

open System
open Xunit


open LazyMatlab
open FSMatlab.InterfaceTypes
open LazyMatlab.InterfaceTypeExtensionsAndHelpers

open TestHelpers

[<Fact>]
let ``simple function calls should execute and return a single correct answer`` () =
    AssertNoVariableChanges (fun _ ->
        let appliedHandle = Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) 
        let resultVarName = "test1_output"
        let [| res_var |] = appliedHandle.Execute([|resultVarName|]) // You must name the output on the matlab side
        let res_untyped = res_var.GetUntyped() :?> double
        let res_typed : double = res_var.Get()
        Assert.Equal(3.0, res_untyped)
        Assert.Equal(3.0, res_typed)
        Assert.Equal<string>(resultVarName, res_var.Name)
        res_var.Delete()
        AssertVariableIsDeleted(res_var.Name)
    )

[<Fact>]
let ``simple function calls should execute and return a single correct answer with helpers`` () =
    TestHelpers.AssertNoVariableChanges (fun _ ->
        let (E1(res_var)) = Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) 
        let res_untyped = res_var.GetUntyped() :?> double
        let res_typed : double = res_var.Get()
        Assert.Equal(3.0, res_untyped)
        Assert.Equal(3.0, res_typed)
        res_var.Delete()
        AssertVariableIsDeleted(res_var.Name)
    )

[<Fact>]
let ``simple function calls should execute and return a single correct answer with execute and retrieve helper`` () =
    TestHelpers.AssertNoVariableChanges (fun _ -> 
        let (ER1(res_untyped)) = Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) 
        Assert.Equal(3.0, res_untyped :?> double)
    )

[<Fact>]
let ``simple function calls should work with matlab-side bound variables`` () =
    TestHelpers.AssertNoVariableChanges (fun _ ->
        let nine = FSMatlab.MatlabInterface.executor.OverwriteVariable("nine", 9.0)
        let two  = FSMatlab.MatlabInterface.executor.OverwriteVariable("two",  2.0)
        let (ER1(res_untyped)) = Toolboxes.``matlab\elfun``.nthroot(nine, two) 
        [ nine; two; ] |> List.iter (fun v -> v.Delete())
        Assert.Equal(3.0, res_untyped :?> double)
    )
