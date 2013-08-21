module Matlab.TypeProvider.MatlabDataSize.Tests

open System
open Xunit


open FSMatlab
open FSMatlab.InterfaceTypes

open TestHelpers

[<Fact>]
let ``a large matrix of doubles should set properly`` () =
    let m_height = 10000
    let m_width = 5000
    let largeMatrix = Array2D.create m_height m_width 5.0
    AssertNoVariableChanges (fun _ ->
        let handle = Data.UnsafeOverwriteVariable "lm" largeMatrix 
        let size = handle.Info.MatlabVariableInfo.Size
        Assert.Equal(m_height, size.[0])
        Assert.Equal(m_width, size.[1])
        //let res = FSMatlab.MatlabInterface.executor.Execute("all(all(" + handle.Name + "== 5.0))")
        //Assert.Equal(1, res.Get())
        //res.DeleteVariable()
        handle.DeleteVariable()
    )

[<Fact>]
let ``a large matrix of complex values should set properly`` () =
    let m_height = 5000
    let m_width = 5000
    let largeMatrix = Array2D.create m_height m_width (new System.Numerics.Complex(5.0, 5.0))
    AssertNoVariableChanges (fun _ ->
        let handle = Data.UnsafeOverwriteVariable "lm" largeMatrix 
        let size = handle.Info.MatlabVariableInfo.Size
        Assert.Equal(m_height, size.[0])
        Assert.Equal(m_width, size.[1])
        handle.DeleteVariable()
    )