module Matlab.COMTypeProvider.Tests

open Parsing
open FSMatlab.FunctionParsing

open Xunit

let complexCommentMatlabFunction = StringWindow("""function [ output_args ... anything after is a comment
    %{ 
        comment here too 
    %}
    ] = Untitled5 ... 
    %{ 
        comment bla
    %}
    ( input_args ) % final""", 0u)

let newlineCommentMatlabFunction = StringWindow("""function [ output_args ... anything after is a comment
    ] = Untitled5 ... 
    ( input_args ) % final""", 0u)

let ohGodWhy = StringWindow("""function [ output_args, ...  anything after is a comment
    %{ 
        comment here too 
    %}
    opa2 ] = Untitled5 ... 
    %{ 
        comment bla
    %}
    ( input_args, in2, ...
    in3 ) % final""", 0u)

[<Fact>]
let ``Should remove comments from function with complex comments`` () = 
    let tokens = matlabTokenize complexCommentMatlabFunction |> Seq.map fst
    let commentFreeTokens = tokens |> removeBothersomeMatlabComments
    let expected = [|"function"; "["; "output_args"; "]"; "="; "Untitled5"; "("; "input_args"; ")"; "%"; "final"|]
    Assert.Equal<string array>(commentFreeTokens |> Seq.toArray, expected)

[<Fact>]
let ``Should parse matlab function with complex comments`` () =
    let name, inargs, outargs = parseFunDecl complexCommentMatlabFunction
    Assert.Equal<string>(name, "Untitled5")
    Assert.Equal<string list>(inargs, ["input_args"])
    Assert.Equal<string list>(outargs, ["output_args"])

[<Fact>]
let ``Should parse matlab function with just newline comments`` () =
    let name, inargs, outargs = parseFunDecl newlineCommentMatlabFunction
    Assert.Equal<string>(name, "Untitled5")
    Assert.Equal<string list>(inargs, ["input_args"])
    Assert.Equal<string list>(outargs, ["output_args"])

[<Fact>]
let ``Should parseMatlab function with many input and output args mixed with comments`` () =
    let name, inargs, outargs = parseFunDecl ohGodWhy
    Assert.Equal<string>(name, "Untitled5")
    Assert.Equal<string list>(inargs, ["input_args"; "in2"; "in3"])
    Assert.Equal<string list>(outargs, ["output_args"; "opa2"])

    