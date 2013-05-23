module Parsing

open System
open System.Collections.Generic

type StringWindow =
    struct
        // These are mutable to prevent property generation.
        val mutable Text: string 
        val mutable Offset: uint32
        new(text: string, offset: uint32) = { Text = text; Offset = offset }
    end
    with 
        /// Determines if the window starts with the given string
        member t.StartsWith (str: string) =            
            let text = t.Text
            let offset = t.Offset
            let textLen = t.Length
            let rec matches (i: uint32) res =
                if i >= textLen then false
                else
                    let newres = res && (text.[int <| offset + i] = str.[int i])
                    match i, newres with | 0u, _ -> newres | _, false -> newres | _ -> matches (i - 1u) newres
            matches (uint32 str.Length - 1u) true
        /// Number of chars still in the window
        member t.Length = (uint32 t.Text.Length) - t.Offset
        /// Creates a new window with an increased offset
        member t.Subwindow start = StringWindow (t.Text, t.Offset + start)
        /// Creates a substring from a subset of the window
        member t.Substring (index, len) = t.Text.Substring(int <| t.Offset + index, int len)
        /// Creates a substring from a subset of the window indexed from 0
        member t.Substring (len) = t.Text.Substring(int <| t.Offset, int len)
        /// Finds the relative index of the given pattern
        member t.IndexOf (pattern: string) : int = max (t.Text.IndexOf(pattern, int t.Offset) - int t.Offset) -1 
        /// Finds the relative index of any of the given chars
        member t.IndexOfAny (patterns: char []) : int = max (t.Text.IndexOfAny(patterns, int t.Offset) - int t.Offset) -1
        /// Indexes to nth char from this offset
        member t.Item with get(x: uint32) : char = t.Text.[int (x + t.Offset)]
        /// Convert window back to a string
        override t.ToString () = t.Text.Substring(int t.Offset) 
        /// Returns a window after the next instance of the pattern
        member t.WindowAfterIndexOf(pattern: string) =
            let idx = t.IndexOf(pattern) in 
            if idx >= 0 then Some <| t.Subwindow(uint32 (idx + pattern.Length)) else None
        /// Is this string window at the start of the string?
        member t.AtStart = t.Offset = 0u
        /// Return a new string window at the start of the string
        member t.Reset() = StringWindow(t.Text, 0u)

let inline (|Consume|_|) (pattern: string) (text: StringWindow) =
    if text.StartsWith(pattern) then Some <| text.Subwindow(uint32 pattern.Length)
    else None

let lazyTokenizeStringWindow (tokenDelims: string []) (ignoreDelims: char []) (window: StringWindow) =
    let cTokenDelims = tokenDelims |> Array.map (fun str -> str.[0])
    let allDelims = Array.concat [cTokenDelims; ignoreDelims]
    let rec parseIgnores (window: StringWindow) =         
        if window.Length > 0u && ignoreDelims |> Array.exists (fun c -> c = window.[0u]) then
            parseIgnores (window.Subwindow(1u))
        else window
    let rec parseTokens (window: StringWindow) =         
        //printfn "Started with: %s" (window.ToString())
        seq {
            if window.Length > 0u then
                // Find first important char
                let parseStartWin = parseIgnores window
   
                // Find where to stop and yield if not empty or a delim
                let nextTokenIdx = parseStartWin.IndexOfAny(allDelims)
                if nextTokenIdx > 0 then
                    let str = parseStartWin.Substring(nextTokenIdx)
                    //printfn "non-delim token: %s" str
                    yield str, parseStartWin

                if nextTokenIdx <> -1 then
                    // Find which delim caused the match and yield if appropriate, then recurse                
                    let nextTokenWin = parseStartWin.Subwindow(uint32 nextTokenIdx)
                    match tokenDelims |> Array.tryFind (fun tkn -> nextTokenWin.StartsWith(tkn)) with
                    | Some (matched) -> //printfn "delim token: %s" matched 
                                        yield matched, nextTokenWin; 
                                        yield! parseTokens (nextTokenWin.Subwindow(uint32 matched.Length))
                    | None -> match ignoreDelims |> Array.tryFind (fun c -> nextTokenWin.[0u] = c) with
                                // Must be an ignore delim
                                | Some (c) ->  
                                    //printfn "ignore token: %A" c
                                    yield! parseTokens (nextTokenWin.Subwindow(1u)) 
                                // Must be end               
                                | None -> ()
                else yield parseStartWin.Substring(int parseStartWin.Length), parseStartWin
        }
    parseTokens window

let inline (|ConsumeToken|_|) (pattern: string) (tokens: IEnumerator<string>) =
    if tokens.Current = pattern then Some ()
    else None

/// tags tokens for inside and outside of the given patterns (outside is false, inside is true)
let tagSeriesFromTokenStream (start: string array) (stop: string array) (tokens: string seq) : (string * bool) seq =
    let enumer = tokens.GetEnumerator()
    let rec inner (startMatched: int) (stopMatched: int) (stack: string list) =
        seq {
            let hasNext = enumer.MoveNext()            
            if hasNext then 
                let current = enumer.Current
                //printf "current %s: -- " (current.Replace("\n", "<n>"))
                match startMatched, stopMatched with
                // Looking for the stop pattern
                | i, j when i = start.Length ->
                    match current with
                    | x when x = stop.[j] -> 
                        //printfn "stopmatch: %i" j
                        if j + 1 = stop.Length then 
                            yield current, true
                            yield! inner 0 0 []
                        else 
                            yield current, true
                            yield! inner i (j + 1) []
                    | _ ->
                        //printfn "nostopmatch: %i" j
                        yield current, true
                        yield! inner i 0 []
                // Looking for start pattern
                | i, j when start.[i] = current ->
                    //printfn "startmatch: %i" i
                    if i + 1 = start.Length then 
                        yield! stack |> List.rev |> List.map (fun v -> v, true)
                        yield current, true                        
                        yield! inner (i + 1) 0 ([])
                    else                        
                        yield! inner (i + 1) 0 (current :: stack)
                // Just moving along
                | i, j -> 
                    //printfn "none"
                    yield! stack |> List.rev |> List.map (fun v -> v, false)
                    yield current, false
                    yield! inner 0 0 []
        }
    inner 0 0 []

