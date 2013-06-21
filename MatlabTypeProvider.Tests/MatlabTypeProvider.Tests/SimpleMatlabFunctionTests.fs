module Matlab.TypeProvider.Functions.Tests

open System
open Xunit

open SimpleMatlab

[<Fact>]
let ``simple function calls should execute and return a single correct answer`` () =
    let res = Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) :?> double
    Assert.Equal(3.0, res)

[<Fact>]
let ``function calls with arrays should execute and return a single correct answer`` () =
    let res = Toolboxes.``matlab\elfun``.nthroot([|9.0; 49.0|], 2.0) 
    Assert.Equal<double []>([|3.0; 7.0|], res :?> double [])

[<Fact>]
let ``function calls with varargsin should work correctly`` () = 
    let res = Toolboxes.``matlab\strfun``.strcat("one", "two", "three")
    Assert.Equal<string>("onetwothree", res :?> string)

// TODO: find an actual varargsout function 
//[<Fact>]
//let ``function calls with varargsout should work correctly`` () = 
//    let [| res |] = Toolboxes.``matlab\elfun``.cos([|0.0|])
//    Assert.Equal<double>(1.0, res :?> double )

[<Fact>]
let ``function calls that expect a matrix should work correctly`` () = 
    let res = Toolboxes.``matlab\matfun``.trace(Array2D.create 5 5 1)
    Assert.Equal<double>(5.0, res :?> double)

[<Fact>]
let ``function calls that return a vector should work correctly`` () =
    let testVal = [|0.0; 0.0; 0.0|]
    let res = Toolboxes.``matlab\elfun``.cos(testVal)
    Assert.Equal<double []>([|1.0; 1.0; 1.0|], res :?> (double[]) )

[<Fact>]
let ``function calls that return a matrix should work correctly`` () =
    let testVal = Array2D.create 5 5 0.0
    let expectedVal = Array2D.create 5 5 1.0
    let res = Toolboxes.``matlab\elfun``.cos(testVal :> obj)
    Assert.Equal<double [,]>(expectedVal, res :?> (double[,]) )