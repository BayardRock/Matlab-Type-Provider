module FSMatlab.FunctionParsing

open ParsingHelpers
open System

let matlabTokenize = lazyTokenizeStringWindow [|"["; "]"; "="; "("; ")"; ","; "%{"; "%}"; "%"; "..."; "\n"|] [|' '; '\r'|]
    
let removeBothersomeMatlabComments tokens = 
    let tagged = 
        [|
            tagSeriesFromTokenStream [|"..."|] [|"\n"|] 
            tagSeriesFromTokenStream [|"\n"; "%{"; "\n"|] [|"\n"; "%}"; "\n"|]
        |] 
        |> Array.map (fun f -> f tokens)
      
    let aggregated = 
        seq {
            let enums = tagged |> Array.map (fun t -> t.GetEnumerator()) 
            let tokens = tokens.GetEnumerator()
            while enums |> Array.map (fun e -> e.MoveNext()) |> Array.fold (&&) (tokens.MoveNext()) do
                let inside = 
                    enums 
                    |> Array.map (fun e -> let _, v = e.Current in v) 
                    |> Array.reduce (||)
                yield tokens.Current, inside
        }
    aggregated |> Seq.filter (fun (token,inside) -> not inside) |> Seq.map fst

/// Finds the next possible function in the file
let findFunc (window: StringWindow) =
    let funcStr = "function"
    // file starts with decl
    if window.AtStart && window.StartsWith(funcStr) then Some(window)
    // maybe another decl further down
    else 
        let ow = window.IndexOf("\n" + "function") in 
            if ow = -1 then None
            else Some(window.Subwindow(uint32 ow))

/// Must start with a letter and then be a mix of letters, numebers or underscores
let isValidMatlabVarName (token: string) = 
    token.Length > 0 
    && Char.IsLetter(token.[0])
    && token.ToCharArray() |> Array.forall (fun c -> Char.IsLetterOrDigit(c) || c = '_')

open System.Collections.Generic

let parseFunDecl (window: StringWindow) =
    let parseMultiCodomain (tokens: IEnumerator<string>) =
        let rec parseVar (tokens: IEnumerator<string>) =
            let current = tokens.Current
            do tokens.MoveNext() |> ignore
            match current with
            | x when isValidMatlabVarName x -> x :: parseCommaOrEnd tokens
            | x -> failwith (sprintf "Unsupported variable name found in codomian: %s" x)
        and parseCommaOrEnd (tokens: IEnumerator<string>) =
            let current = tokens.Current
            do tokens.MoveNext() |> ignore
            match current with 
            | "]" -> []
            | "," -> parseVar tokens
            | x -> failwith (sprintf "Unexpected token found in codomain: %s" x)
        parseVar tokens

    let parseCodomain (tokens: IEnumerator<string>) =
        let current = tokens.Current
        do tokens.MoveNext() |> ignore
        match current with
        | x when isValidMatlabVarName x -> [x]
        | "[" -> parseMultiCodomain tokens
        | x -> failwith (sprintf "Unexpected token found at beginning of codomain: %s" x)

    let parseFunctionName (tokens: IEnumerator<string>) =
        let current = tokens.Current
        do tokens.MoveNext() |> ignore
        match current with 
        | "=" ->
            let current = tokens.Current 
            do tokens.MoveNext() |> ignore
            match current with
            | x when isValidMatlabVarName x -> x
            | x -> (sprintf "Unsupported function name found: %s" x)
        | t -> failwith (sprintf "Unexpected character when trying to parse function name: %s" t)

    let parseDomain (tokens: IEnumerator<string>) =
        let rec parseVar (tokens: IEnumerator<string>) =
            let current = tokens.Current
            tokens.MoveNext() |> ignore
            match current with
            | x when isValidMatlabVarName x -> x :: parseCommaOrEnd tokens
            | x -> failwith (sprintf "Unsupported variable name found in codomian: %s" x)
        and parseCommaOrEnd (tokens: IEnumerator<string>) =
            let current = tokens.Current
            tokens.MoveNext() |> ignore
            match current with 
            | ")" -> []
            | "," -> parseVar tokens
            | x -> failwith (sprintf "Unexpected token found in codomain: %s" x)
        let current = tokens.Current
        do tokens.MoveNext() |> ignore
        match current with
        | "(" -> parseVar tokens
        | x -> failwith (sprintf "Unexpected token found at beginning of domain: %s" x)

    let cleanTokens = window |> matlabTokenize |> Seq.map fst |> removeBothersomeMatlabComments
    let enumTokens = cleanTokens.GetEnumerator()
    do enumTokens.MoveNext() |> ignore
    let current = enumTokens.Current
    do enumTokens.MoveNext() |> ignore
    match current with
    | "function" -> 
        let codomain = parseCodomain enumTokens
        let funname = parseFunctionName enumTokens
        let domain = parseDomain enumTokens
        (funname, domain, codomain)
    | _ -> failwith "function token expected but not found"


//    let rec parseFunDecls (window: StringWindow) =
//        [
//            match findFunc(window) with
//            | Some (tokens) -> 
//                yield parseFunDecl (tokens |> Seq.map fst)
//                yield! parseFunDecls (window.Subwindow(uint32 "function".Length))
//            | None -> ()
//        ] 

/// Finds all matlab function declarations in the file and parses them
//let rec findDecl (window: StringWindow) =
//    [
//        match findFunc(window) with
//        | Some tokenStream ->                
//            let eqIdx = window.IndexOf("=") 
//            let codomainPrms = window.Substring(eqIdx).Split([|'[';']';',';' '|], StringSplitOptions.RemoveEmptyEntries)
//
//            let eqWindow = window.Subwindow(uint32 eqIdx + 1u)
//            let varsStartIdx = eqWindow.IndexOf("(")
//            let funName = eqWindow.Substring(varsStartIdx)
//
//            let doWindow = eqWindow.Subwindow(uint32 varsStartIdx + 1u)
//            let domainEndIdx = doWindow.IndexOf(")")
//            let domainPrms = doWindow.Substring(domainEndIdx).Split([|'(';')';',';' '|], StringSplitOptions.RemoveEmptyEntries)
//                
//            yield funName, domainPrms, codomainPrms
//            yield! findDecl doWindow                                            
//        | None -> ()
//    ]
