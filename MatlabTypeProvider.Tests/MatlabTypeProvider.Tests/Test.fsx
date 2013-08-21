
open FSMatlab

let x = Vars.x
let m = Vars.m
let v = Vars.v
let v2 = Vars.v2

// Simple types
Vars.matrix
Vars.vector
Vars.vectorT

// Standard Function Call
let x = Toolboxes.``matlab\elfun``.nthroot(2.0, 1.0)

// Complex Types
Vars.imag_matrix
Vars.imag_vector
Vars.imag_vectorT
Vars.imag_single

// Varargin Function Call

