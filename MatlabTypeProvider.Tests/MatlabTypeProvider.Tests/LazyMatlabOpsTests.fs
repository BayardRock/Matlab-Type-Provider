module Matlab.TypeProvider.LazyOps.Tests

open System
open Xunit


open FSMatlab
open FSMatlab.InterfaceTypes

open TestHelpers

[<Fact>]
let ``addition should work on set matlab variables`` () =
    AssertNoVariableChanges (fun _ ->
        use v1 = Data.UnsafeOverwriteVariable "one_0" 1.0
        use v2 = Data.UnsafeOverwriteVariable "two_0" 2.0
        use v3 = Data.UnsafeOverwriteVariable "three_0" 3.0
        let exp = Exec.EvaluateExpression (v1 + v2 + v3) :?> double
        Assert.Equal<double>(exp, 6.0)
    )

[<Fact>]
let ``subtraction should work on set matlab variables`` () =
    AssertNoVariableChanges (fun _ ->
        use v1 = Data.UnsafeOverwriteVariable "one_1" 1.0
        use v2 = Data.UnsafeOverwriteVariable "two_1" 2.0
        use v3 = Data.UnsafeOverwriteVariable "three_1" 3.0
        let exp = Exec.EvaluateExpression (v1 - v2 - v3) :?> double
        Assert.Equal<double>(exp, -4.0)
    )

[<Fact>]
let ``multiplication should work on set matlab variables`` () =
    AssertNoVariableChanges (fun _ ->
        use v1 = Data.UnsafeOverwriteVariable "one_2" 1.0
        use v2 = Data.UnsafeOverwriteVariable "two_2" 3.0
        use v3 = Data.UnsafeOverwriteVariable "three_2" 3.0
        let exp = Exec.EvaluateExpression (v1 * v2 * v3) :?> double
        Assert.Equal<double>(exp, 9.0)
    )

[<Fact>]
let ``divison should work on set matlab variables`` () =
    AssertNoVariableChanges (fun _ ->
        use v1 = Data.UnsafeOverwriteVariable "one_3" 6.0
        use v2 = Data.UnsafeOverwriteVariable "two_3" 2.0
        let exp = Exec.EvaluateExpression (v1 / v2) :?> double
        Assert.Equal<double>(exp, 3.0)
    )

[<Fact>]
let ``dot multiply should work on set matlab variables`` () =
    AssertNoVariableChanges (fun _ ->
        use v1 = Data.UnsafeOverwriteVariable "one_4" [|1.0; 2.0; 3.0|]
        use v2 = Data.UnsafeOverwriteVariable "two_4" 2.0
        let exp = Exec.EvaluateExpression (v1 .* v2) :?> double[]
        Assert.Equal<double[]>(exp, [|2.0; 4.0; 6.0|])
    )

[<Fact>]
let ``dot divide should work on set matlab variables`` () =
    AssertNoVariableChanges (fun _ ->
        use v1 = Data.UnsafeOverwriteVariable "one_5" [|2.0; 4.0; 6.0|]
        use v2 = Data.UnsafeOverwriteVariable "two_5" 2.0
        let exp = Exec.EvaluateExpression (v1 ./ v2) :?> double[]
        Assert.Equal<double[]>(exp, [|1.0; 2.0; 3.0|])
    )

[<Fact>]
let ``dot power should work on set matlab variables`` () =
    AssertNoVariableChanges (fun _ ->
        use v1 = Data.UnsafeOverwriteVariable "one_6" [|2.0; 4.0; 6.0|]
        use v2 = Data.UnsafeOverwriteVariable "two_6" 2.0
        let exp = Exec.EvaluateExpression (v1 .^ v2) :?> double[]
        Assert.Equal<double[]>(exp, [|4.0; 16.0; 36.0|])
    )