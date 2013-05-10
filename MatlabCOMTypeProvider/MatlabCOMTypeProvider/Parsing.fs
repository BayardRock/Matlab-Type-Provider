module Parsing

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
        /// Indexes to nth char from this offset
        member t.Item with get(x: uint32) : char = t.Text.[int (x + t.Offset)]
        /// Convert window back to a string
        override t.ToString () = t.Text.Substring(int t.Offset) 
        /// Returns a window after the next instance of the pattern
        member t.WindowAfterIndexOf(pattern: string) =
            let idx = t.IndexOf(pattern) in 
            if idx >= 0 then Some <| t.Subwindow(uint32 (idx + pattern.Length)) else None
let inline (|Consume|_|) (pattern: string) (text: StringWindow) =
    if text.StartsWith(pattern) then Some <| text.Subwindow(uint32 pattern.Length)
    else None
