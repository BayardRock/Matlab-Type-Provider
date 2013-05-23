module Matlab.TypeProvider.Functions.Tests

open System
open Xunit

[<Fact>]
let ``simple function calls should execute and return a single correct answer`` () =
    let res = Matlab.Toolboxes.``matlab\elfun``.nthroot(9.0, 2.0) :?> double
    Assert.Equal(3.0, res)

// Returning a 2d array... 
[<Fact>]
let ``function calls with arrays should execute and return a single correct answer`` () =
    let res = Matlab.Toolboxes.``matlab\elfun``.nthroot([|9.0; 49.0|], 2.0) 
    Assert.Equal<double []>([|3.0; 7.0|], res :?> double [])


