namespace FSMatlab.LazyProviderTypes

open FSMatlab.InterfaceTypes

module LazyProviderTypeHelpers =
    let processId = lazy System.Diagnostics.Process.GetCurrentProcess().Id.ToString()

    let getRandomVariableName () = 
        let procid = processId.Force()
        let randomAddendum = System.IO.Path.GetRandomFileName().Replace('.', '_')
        System.String.Format("mtp_{0}_{1}", procid, randomAddendum)



