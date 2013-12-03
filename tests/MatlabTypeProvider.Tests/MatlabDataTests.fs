module Matlab.TypeProvider.MatlabDataSize.Tests

open System
open System.Numerics

open Xunit

open FSMatlab
open FSMatlab.InterfaceTypes

open TestHelpers

#nowarn "10001" // Speical warning for Type Provider internals 



[<Fact>] 
let ``supported instance types should round trip`` () =
    // Matlab supported basic types: 
    // double, single, int8, int16, int32, int64, uint8, uint16, uint32, uint64

    let vs : (obj) list =
        [
            // Logical Types
            true; false; [|true; true; true|]; [|false; false; false|]; (Array2D.create 2 2 false); (Array2D.create 2 2 true);
            // Double Types
            0.0; 1.5; [|0.0; 0.0; 0.0|]; [|0.1; 0.2; 0.3|]; (Array2D.create 2 2 0.0); (Array2D.create 2 2 1.5);
            // Single Types
            0.0f; 1.5f; [|0.0f; 0.0f; 0.0f|]; [|0.1f; 0.2f; 0.3f|]; (Array2D.create 2 2 0.0f); (Array2D.create 2 2 1.5f);
            // Complex Numer Types (Note: Matlab does not accept 0 values)
            // Note -- Not yet supported: Complex(2.0, 2.0); [|Complex(0.1, 0.2); Complex(3.0,0.4); Complex(5.0,6.0)|]; 
            (Array2D.create 2 2 (Complex(1.0, 2.0))); 
        ] 
    let vsm = vs |> Seq.mapi (fun i v -> i, v)
    let testPrefix = "typeRoundTripTest"
    AssertNoVariableChanges (fun _ ->
        for i, v in vsm do 
            use handle = Data.UnsafeOverwriteVariable (testPrefix + i.ToString()) v
            Assert.Equal(v, handle.GetUntyped())
    )

[<Fact>]
let ``a large matrix of doubles should set properly`` () =
    let m_height = 10000
    let m_width = 5000
    let largeMatrix = Array2D.create m_height m_width 5.0
    AssertNoVariableChanges (fun _ ->
        use handle = Data.UnsafeOverwriteVariable "lm" largeMatrix 
        let size = handle.Info.MatlabVariableInfo.Size
        Assert.Equal(m_height, size.[0])
        Assert.Equal(m_width, size.[1])
        // assure the correct values
        use res = FSMatlab.MatlabInterface.executor.Execute("all(all(" + handle.Name + "== 5.0))")
        Assert.True(res.Get())
    )

[<Fact>]
let ``a large matrix of complex values should set properly`` () =
    let m_height = 5000
    let m_width = 5000
    let largeMatrix = Array2D.create m_height m_width (new System.Numerics.Complex(2.0, 3.0))
    AssertNoVariableChanges (fun _ ->
        use handle = Data.UnsafeOverwriteVariable "lm" largeMatrix 
        let size = handle.Info.MatlabVariableInfo.Size
        Assert.Equal(m_height, size.[0])
        Assert.Equal(m_width, size.[1])
        // assure the correct values
        use res = FSMatlab.MatlabInterface.executor.Execute("all(all(" + handle.Name + "== 2+3i))")
        Assert.True(res.Get())
    )

[<Fact>]
let ``a large matrix in matlab should get properly`` () =
    AssertNoVariableChanges (fun _ ->
        use ones = FSMatlab.MatlabInterface.executor.Execute("ones(10000,5000)")
        let v : double [,] = ones.Get()
        Assert.Equal(v.GetLength(0), 10000)
        Assert.Equal(v.GetLength(1), 5000)
        do v |> Array2D.iter (fun v -> Assert.Equal(1.0, v))
        Assert.Equal(10000.0 * 5000.0, v |> Seq.cast<double> |> Seq.sum)
    )

[<Fact>]
let ``a large matrix of complex numbers in matlab should get properly`` () =
    AssertNoVariableChanges (fun _ ->
        use ones = FSMatlab.MatlabInterface.executor.Execute("ones(10000,5000) * (1 + 2i)")
        let v : Complex [,] = ones.Get()
        Assert.Equal(v.GetLength(0), 10000)
        Assert.Equal(v.GetLength(1), 5000)
        do v |> Array2D.iter (fun v -> Assert.Equal(Complex(1.0, 2.0), v))
    )

[<Fact>]
let ``a large matrix of logicals in matlab should get properly`` () =
    AssertNoVariableChanges (fun _ ->
        use ones = FSMatlab.MatlabInterface.executor.Execute("logical(ones(10000,5000))")
        let v : bool [,] = ones.Get()
        Assert.Equal(v.GetLength(0), 10000)
        Assert.Equal(v.GetLength(1), 5000)
        do v |> Array2D.iter (fun v -> Assert.Equal(true, v))
    )
    