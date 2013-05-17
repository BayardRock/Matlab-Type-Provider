
#r """..\MatlabCOMTypeProvider\bin\Debug\MatlabCOMTypeProvider.dll"""

let x = Matlab.Vars.x
let m = Matlab.Vars.m
let v = Matlab.Vars.v
let v2 = Matlab.Vars.v2

// Simple types
Matlab.Vars.matrix
Matlab.Vars.vector
Matlab.Vars.vectorT

// Standard Function Call
let x = Matlab.Toolboxes.``matlab\elfun``.nthroot(2.0, 1)

// Complex Types
Matlab.Vars.imag_matrix
Matlab.Vars.imag_vector
Matlab.Vars.imag_vectorT
Matlab.Vars.imag_single


