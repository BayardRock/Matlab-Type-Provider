[<AutoOpen>]
module FSMatlab.InterfaceTypeExtensionsAndHelpers
#nowarn "25" // Yes, I know that they're incomplete pattern matches

open FSMatlab.InterfaceTypes

type MatlabVariableHandle with
    /// Safely gets the contents of the variable.  
    /// If anything goes wrong None will be returned, including the case where Matlab is no longer available.
    member t.GetSafe () : Option<'a> = 
        try 
            if typeof<'a> = t.Info.Type 
            then Some(t.GetUntyped() :?> 'a) else None
        with _ -> None
    /// Gets the contents of the variable statically parameterized by type
    member t.Get () : 'a = t.GetUntyped() :?> 'a

type MatlabAppliedFunctionHandle with
    /// Similar to execute, but gets the actual contents of the variables. This version expects an number of output args that will be randomly named. 
    /// The variables will be deleted after retrieval.
    member t.ExecuteAndRetrieve(n: int) : obj [] =
        let vars = t.ExecuteNumbered (n) 
        let res = vars |> Array.map (fun mv -> mv.GetUntyped())
        do vars |> Array.iter (fun mv -> mv.DeleteVariable())
        res

/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E1 (result: MatlabAppliedFunctionHandle) = let [| r |] = result.ExecuteNumbered(1) in r
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E2 (result: MatlabAppliedFunctionHandle) = let [| r1; r2 |] = result.ExecuteNumbered(2) in (r1,r2)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E3 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3 |] = result.ExecuteNumbered(3) in (r1,r2,r3)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E4 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4 |] = result.ExecuteNumbered(4) in (r1,r2,r3,r4)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E5 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5 |] = result.ExecuteNumbered(5) in (r1,r2,r3,r4,r5)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E6 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6 |] = result.ExecuteNumbered(6) in (r1,r2,r3,r4,r5,r6)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E7 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7 |] = result.ExecuteNumbered(7) in (r1,r2,r3,r4,r5,r6,r7)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E8 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8 |] = result.ExecuteNumbered(8) in (r1,r2,r3,r4,r5,r6,r7,r8)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let E9 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8; r9 |] = result.ExecuteNumbered(9) in (r1,r2,r3,r4,r5,r6,r7,r8,r9)

/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E1|) (result: MatlabAppliedFunctionHandle) = let [| r |] = result.ExecuteNumbered(1) in r
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E2|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2 |] = result.ExecuteNumbered(2) in (r1,r2)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E3|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3 |] = result.ExecuteNumbered(3) in (r1,r2,r3)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E4|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4 |] = result.ExecuteNumbered(4) in (r1,r2,r3,r4)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E5|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5 |] = result.ExecuteNumbered(5) in (r1,r2,r3,r4,r5)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E6|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6 |] = result.ExecuteNumbered(6) in (r1,r2,r3,r4,r5,r6)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E7|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7 |] = result.ExecuteNumbered(7) in (r1,r2,r3,r4,r5,r6,r7)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E8|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8 |] = result.ExecuteNumbered(8) in (r1,r2,r3,r4,r5,r6,r7,r8)
/// These helpers execute the matlab function and split out the MatlabVariableHandles into tuples
let (|E9|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8; r9 |] = result.ExecuteNumbered(9) in (r1,r2,r3,r4,r5,r6,r7,r8,r9)

/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG1 (result: MatlabAppliedFunctionHandle) = let [| r |] = result.ExecuteAndRetrieve(1) in r :?> 'a
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG2 (result: MatlabAppliedFunctionHandle) = let [| r1; r2 |] = result.ExecuteAndRetrieve(2) in r1, r2
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG3 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3 |] = result.ExecuteAndRetrieve(3) in (r1,r2,r3)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG4 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4 |] = result.ExecuteAndRetrieve(4) in (r1,r2,r3,r4)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG5 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5 |] = result.ExecuteAndRetrieve(5) in (r1,r2,r3,r4,r5)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG6 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6 |] = result.ExecuteAndRetrieve(6) in (r1,r2,r3,r4,r5,r6)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG7 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7 |] = result.ExecuteAndRetrieve(7) in (r1,r2,r3,r4,r5,r6,r7)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG8 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8 |] = result.ExecuteAndRetrieve(8) in (r1,r2,r3,r4,r5,r6,r7,r8)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EG9 (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8; r9 |] = result.ExecuteAndRetrieve(9) in (r1,r2,r3,r4,r5,r6,r7,r8,r9)


/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG1|) (result: MatlabAppliedFunctionHandle) = let [| r |] = result.ExecuteAndRetrieve(1) in r
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG2|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2 |] = result.ExecuteAndRetrieve(2) in (r1,r2)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG3|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3 |] = result.ExecuteAndRetrieve(3) in (r1,r2,r3)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG4|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4 |] = result.ExecuteAndRetrieve(4) in (r1,r2,r3,r4)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG5|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5 |] = result.ExecuteAndRetrieve(5) in (r1,r2,r3,r4,r5)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG6|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6 |] = result.ExecuteAndRetrieve(6) in (r1,r2,r3,r4,r5,r6)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG7|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7 |] = result.ExecuteAndRetrieve(7) in (r1,r2,r3,r4,r5,r6,r7)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG8|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8 |] = result.ExecuteAndRetrieve(8) in (r1,r2,r3,r4,r5,r6,r7,r8)
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let (|EG9|) (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8; r9 |] = result.ExecuteAndRetrieve(9) in (r1,r2,r3,r4,r5,r6,r7,r8,r9)


/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT1<'a> (result: MatlabAppliedFunctionHandle) = let [| r |] = result.ExecuteAndRetrieve(1) in r :?> 'a
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT2<'a,'b> (result: MatlabAppliedFunctionHandle) = let [| r1; r2 |] = result.ExecuteAndRetrieve(2) in r1 :?> 'a, r2 :?> 'b
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT3<'a,'b,'c> (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3 |] = result.ExecuteAndRetrieve(3) in r1 :?> 'a, r2 :?> 'b, r3 :?> 'c
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT4<'a,'b,'c,'d> (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4 |] = result.ExecuteAndRetrieve(4) in r1 :?> 'a, r2 :?> 'b, r3 :?> 'c, r4 :?> 'd
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT5<'a,'b,'c,'d,'e> (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5 |] = result.ExecuteAndRetrieve(5) in r1 :?> 'a, r2 :?> 'b, r3 :?> 'c, r4 :?> 'd, r5 :?> 'e
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT6<'a,'b,'c,'d,'e,'f> (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6 |] = result.ExecuteAndRetrieve(6) in r1 :?> 'a, r2 :?> 'b, r3 :?> 'c, r4 :?> 'd, r5 :?> 'e, r6 :?> 'f
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT7<'a,'b,'c,'d,'e,'f,'g> (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7 |] = result.ExecuteAndRetrieve(7) in r1 :?> 'a, r2 :?> 'b, r3 :?> 'c, r4 :?> 'd, r5 :?> 'e, r6 :?> 'f, r7 :?> 'g
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT8<'a,'b,'c,'d,'e,'f,'g,'h> (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8 |] = result.ExecuteAndRetrieve(8) in r1 :?> 'a, r2 :?> 'b, r3 :?> 'c, r4 :?> 'd, r5 :?> 'e, r6 :?> 'f, r7 :?> 'g, r8 :?> 'h
/// These helpers allow you to execute and split out the results easily, getting the actual values and deleting the temporary matlab-side variables
let EGT9<'a,'b,'c,'d,'e,'f,'g,'h,'i> (result: MatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8; r9 |] = result.ExecuteAndRetrieve(9) in r1 :?> 'a, r2 :?> 'b, r3 :?> 'c, r4 :?> 'd, r5 :?> 'e, r6 :?> 'f, r7 :?> 'g, r8 :?> 'h, r9 :?> 'i 
