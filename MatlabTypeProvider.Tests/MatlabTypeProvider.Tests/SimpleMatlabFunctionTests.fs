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
let ``function calls with varargsin should work correctly`` () = ()

[<Fact>]
let ``function calls with varargsout should work correctly`` () = ()
   
[<Fact>]
let ``function calls with internally defined functions should work correctly`` () = ()

[<Fact>]
let ``function calls that expect a vector should work correctly`` () = ()

[<Fact>]
let ``function calls that expect a matrix should work correctly`` () = ()

[<Fact>]
let ``function calls that return a vector should work correctly`` () = ()

[<Fact>]
let ``function calls that return a matrix should work correctly`` () = ()
