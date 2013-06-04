Matlab-Type-Provider
====================

A (not yet complete) Type Provider for Matlab in the spirit of the R Type Provider

Recent Additions:
- 05/31/2013 - Added Basics for the Lazy Provider (open the LazyMatlab namespace instead of SimpleMatlab)
- 05/24/2013 - Added support for varargin/varargout functions and native matlab functions
- 05/17/2013 - Added Matlab->F# introp for complex values, vectors and matrices
- 05/16/2013 - Added support for discovering and calling matlab toolbox functions

See http://bayardrock.github.io/Matlab-Type-Provider for more information.  
Or just ask me if you have any questions: https://github.com/Rickasaurus

### What to expect

The goal of this project is to provide a smooth and efficient framework for F#-Matlab interactions.  The goals for 1.0 include support for most Matlab functions and types in as well typed a manner as possible.

A simple version of our goal is now working fairly well.  Give it a little play and let me know if you find any problems.

### Current Requirements

* Matlab 2013a (or earlier with [strjoin.m](http://www.mathworks.com/matlabcentral/fileexchange/31862-strjoin), tested up to 2012a)
* Visual Studio 2012 (May work with other Windows F# IDEs as well, but not tested)

### Current Capabilities

* Access non-complex Values, Matrices and Vectors in a strongly typed fashion. 
* The ability to call Matlab Toolbox functions with appropriate F# values.

### Getting Started

Add the following parameters to your Matlab 2013a shortcut:

`matlab.exe -automation -desktop`

This will allow you to keep a Matlab session open while you connect and disconnect with Visual Studio. If you don't do this then the Matlab Provider will launch a session when it starts which will close when the Type Provider is unloaded. 

Then you must load the type provider.  This is done either by an assembly reference, if you're using it in your project, or by specifying it like so in your F# .fsx script:

`#R "MatlabTypeProvider.dll"`

Then just access variables that were bound in Matlab before the Type Provider was loaded like so: 

```
open SimpleMatlabProvider
let xFromMatlab = Vars.X
```

You can find supported functions inside of their Matlab toolboxes:

```
open SimpleMatlabProvider
let res = Toolboxes.``matlab\elfun``.nthroot(6.0, 2.0)
```

Varargin and varargout parameters are treated as arrays, and all built-in functions are treated as varargin/varargout. 

```
open SimpleMatlabProvider
let [| res |] = Toolboxes.``matlab\elfun``.cos([|0.0|]) 
```


### Known Problems / Future Work

* The current communication interface is COM, and I expect this puts a limit on the size of things.  I hope to be moving to the dll interface once I get things going a bit more.  In the long run I'd very much like to be able to support MacOS and Linux.
* Currently any call to a Matlab function will return all of its result values, even the optional ones.  I'm currently exploring several approaches to resolving this.
* Toolbox naming is a bit wonky, but allows for easy exploration. 
* The type provider does not currently take any static parameters.  I'd like it to support at least the ability to select the way to contact Matlab and also to execute a specified script to bind variables.
* It would be ideal to be able to compose functions in F# and then have the entire call be executed in Matlab.  This may take some doing, but I believe it's completely possible. 

### Planned Limitations

* The parameters to and the result of calls to Matlab functions will remain untyped for the foreseeable future.  As it currently stands, there seems be no way to provide types for Matlab functions without manual labeling as with TypeScript.
* You can't get any information about the built in functions and so they'll be treated as varargin/varargout for the foreseeable future.
