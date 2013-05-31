[<AutoOpen>]
module LazyMatlab.InterfaceTypeExtensionsAndHelpers

open FSMatlab.InterfaceTypes

type IMatlabVariableHandle with
    /// Safely gets the contents of the variable.  
    /// If anything goes wrong None will be returned, including the case where Matlab is no longer available.
    member t.GetSafe () : Option<'a> = 
        try 
            if typeof<'a> = t.LocalType 
            then Some(t.GetUntyped() :?> 'a) else None
        with _ -> None
    /// Gets the contents of the variable statically parameterized by type
    member t.Get () : 'a = t.GetUntyped() :?> 'a

type IMatlabAppliedFunctionHandle with
    /// Similar to execute, but gets the actual contents of the variables. This version expects an number of output args that will be randomly named. 
    /// The variables will be deleted after retrieval.
    member t.ExecuteAndRetrieve(n: int) : obj [] =
        let vars = t.Execute(n) 
        let res = vars |> Array.map (fun mv -> mv.GetUntyped())
        do vars |> Array.iter (fun mv -> mv.Delete())
        res

//
// These helpers allow you to execute and split out the results easily
//

let (|E1|_|) (result: IMatlabAppliedFunctionHandle) = let [| r |] = result.Execute(1) in Some r
let (|E2|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2 |] = result.Execute(2) in (r1,r2)
let (|E3|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3 |] = result.Execute(3) in (r1,r2,r3)
let (|E4|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4 |] = result.Execute(4) in (r1,r2,r3,r4)
let (|E5|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5 |] = result.Execute(5) in (r1,r2,r3,r4,r5)
let (|E6|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6 |] = result.Execute(6) in (r1,r2,r3,r4,r5,r6)
let (|E7|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7 |] = result.Execute(7) in (r1,r2,r3,r4,r5,r6,r7)
let (|E8|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8 |] = result.Execute(8) in (r1,r2,r3,r4,r5,r6,r7,r8)
let (|E9|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8; r9 |] = result.Execute(9) in (r1,r2,r3,r4,r5,r6,r7,r8,r9)

//
// These helpers allow you to execute and split out the results easily, getting the actual values
//

let (|ER1|) (result: IMatlabAppliedFunctionHandle) = let [| r |] = result.ExecuteAndRetrieve(1) in r
let (|ER2|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2 |] = result.ExecuteAndRetrieve(2) in (r1,r2)
let (|ER3|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3 |] = result.ExecuteAndRetrieve(3) in (r1,r2,r3)
let (|ER4|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4 |] = result.ExecuteAndRetrieve(4) in (r1,r2,r3,r4)
let (|ER5|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5 |] = result.ExecuteAndRetrieve(5) in (r1,r2,r3,r4,r5)
let (|ER6|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6 |] = result.ExecuteAndRetrieve(6) in (r1,r2,r3,r4,r5,r6)
let (|ER7|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7 |] = result.ExecuteAndRetrieve(7) in (r1,r2,r3,r4,r5,r6,r7)
let (|ER8|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8 |] = result.ExecuteAndRetrieve(8) in (r1,r2,r3,r4,r5,r6,r7,r8)
let (|ER9|) (result: IMatlabAppliedFunctionHandle) = let [| r1; r2; r3; r4; r5; r6; r7; r8; r9 |] = result.ExecuteAndRetrieve(9) in (r1,r2,r3,r4,r5,r6,r7,r8,r9)
