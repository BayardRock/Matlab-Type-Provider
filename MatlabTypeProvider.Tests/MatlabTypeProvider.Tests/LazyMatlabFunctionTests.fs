module Matlab.TypeProvider.LazyFunctions.Tests

open System
open Xunit


open FSMatlab
open FSMatlab.InterfaceTypes

open TestHelpers


open Toolboxes.matlab.elfun
[<Fact>]
let ``function calls with arrays should execute and return a single correct answer`` () =
    let res = M.nthroot([|9.0; 49.0|], 2.0) |> EGT1<double []>
    Assert.Equal<double []>([|3.0; 7.0|], res)

[<Fact>]
let ``function calls that return a vector should work correctly`` () =
    let testVal = [|0.0; 0.0; 0.0|]
    let res = M.cos(testVal) |> EGT1<double []>
    Assert.Equal<double []>([|1.0; 1.0; 1.0|], res)

[<Fact>]
let ``function calls that return a matrix should work correctly`` () =
    let testVal = Array2D.create 5 5 0.0
    let expectedVal = Array2D.create 5 5 1.0
    let res = M.cos(testVal :> obj) |> EGT1<double[,]>
    Assert.Equal<double [,]>(expectedVal, res)

[<Fact>]
let ``simple function calls should execute and return a single correct answer`` () =
    AssertNoVariableChanges (fun _ ->
        let appliedHandle = M.nthroot(9.0, 2.0) 
        let resultVarName = "test1_output"
        use res_var = appliedHandle.ExecuteNamed([|resultVarName|]).[0] in // You must name the output on the matlab side
            let res_untyped = res_var.GetUntyped() :?> double
            let res_typed : double = res_var.Get()
            do Assert.Equal(3.0, res_untyped)
               Assert.Equal(3.0, res_typed)
               Assert.Equal<string>(resultVarName, res_var.Name)
    )

[<Fact>]
let ``simple function calls should execute and return a single correct answer with helpers`` () =
    AssertNoVariableChanges (fun _ ->
        let res_var = M.nthroot(9.0, 2.0) |> E1
        use res_var = res_var in
            let res_untyped = res_var.GetUntyped() :?> double
            let res_typed : double = res_var.Get()
            do Assert.Equal(3.0, res_untyped)
               Assert.Equal(3.0, res_typed)
    )

[<Fact>]
let ``simple function calls should execute and return a single correct answer with execute and retrieve helper`` () =
    AssertNoVariableChanges (fun _ -> 
        let (EG1(res_untyped)) = M.nthroot(9.0, 2.0) 
        Assert.Equal(3.0, res_untyped :?> double)
    )

[<Fact>]
let ``simple function calls should work with matlab-side bound variables`` () =
    AssertNoVariableChanges (fun _ ->
        use nine = Data.UnsafeOverwriteVariable "nine" 9.0
        use two  = Data.UnsafeOverwriteVariable "two"  2.0
        let (EG1(res_untyped)) = M.nthroot(nine, two) 
        Assert.Equal(3.0, res_untyped :?> double)
    )


open Toolboxes.matlab.matfun
[<Fact>]
let ``function calls that expect a matrix should work correctly`` () = 
    let res = M.trace(Array2D.create 5 5 1.0) |> EGT1<double>
    Assert.Equal<double>(5.0, res)






open Toolboxes.matlab.elmat
[<Fact>] 
let ``function with two output params should work correctly with lefthand side execute-get`` () =
    AssertNoVariableChanges (fun _ -> 
        let (EG2(m,n)) = M.size([|1.0;2.0;3.0;4.0;5.0|])
        Assert.Equal(1.0, m :?> double)
        Assert.Equal(5.0, n :?> double)
    )

[<Fact>]
let ``function with two output params should work correctly with right out execute-get`` () =  
    AssertNoVariableChanges (fun _ ->
        let m,n = M.size([|1.0;2.0;3.0;4.0;5.0|]) |> EG2
        Assert.Equal(1.0, m :?> double)
        Assert.Equal(5.0, n :?> double)
    )

[<Fact>]
let ``function with two output params should work correctly with right out execute-get-typed`` () =  
    AssertNoVariableChanges (fun _ ->
        let m,n = M.size([|1.0;2.0;3.0;4.0;5.0|]) |> EGT2<double,double>
        Assert.Equal(1.0, m)
        Assert.Equal(5.0, n)
    )

[<Fact>] 
let ``error in matlab computation should cause an appropriate exception`` () =
    AssertNoVariableChanges (fun _ ->
        Assert.Throws<MatlabErrorException>( new Assert.ThrowsDelegate(fun _ ->
            M.nthroot("hello") |> EG1 |> ignore
        )) |> ignore        
    )    

open Toolboxes.matlab.strfun   
[<Fact>]
let ``function calls with varargsin should work correctly`` () =     
    let res = M.strcat("one", "two", "three") |> EGT1<string>
    Assert.Equal<string>("onetwothree", res)