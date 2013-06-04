module Matlab.TypeProvider.LazyFunctions.Tests

open System
open Xunit


open LazyMatlab
open FSMatlab.InterfaceTypes

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
        let (EG1(res_untyped)) = Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) 
        Assert.Equal(3.0, res_untyped :?> double)
    )

[<Fact>]
let ``simple function calls should work with matlab-side bound variables`` () =
    TestHelpers.AssertNoVariableChanges (fun _ ->
        let nine = FSMatlab.MatlabInterface.executor.OverwriteVariable("nine", 9.0)
        let two  = FSMatlab.MatlabInterface.executor.OverwriteVariable("two",  2.0)
        let (EG1(res_untyped)) = Toolboxes.``matlab\elfun``.nthroot(nine, two) 
        [ nine; two; ] |> List.iter (fun v -> v.Delete())
        Assert.Equal(3.0, res_untyped :?> double)
    )


[<Fact>] 
let ``function with two output params should work correctly with lefthand side execute-get`` () =
    let (EG2(m,n)) = Toolboxes.``matlab\elmat``.size([|1.0;2.0;3.0;4.0;5.0|])
    Assert.Equal(1.0, m :?> double)
    Assert.Equal(5.0, n :?> double)

[<Fact>]
let ``function with two output params should work correctly with right out execute-get`` () =  
    let m,n = Toolboxes.``matlab\elmat``.size([|1.0;2.0;3.0;4.0;5.0|]) |> EG2
    Assert.Equal(1.0, m :?> double)
    Assert.Equal(5.0, n :?> double)

[<Fact>]
let ``function with two output params should work correctly with right out execute-get-typed`` () =  
    let m,n = Toolboxes.``matlab\elmat``.size([|1.0;2.0;3.0;4.0;5.0|]) |> EGT2<double,double>
    Assert.Equal(1.0, m)
    Assert.Equal(5.0, n)
