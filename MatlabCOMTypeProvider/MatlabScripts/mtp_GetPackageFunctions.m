function [ functionCsv ] = mtp_GetPackageFunctions( pkgName )
functionCsv = ''
pkgFuncs = meta.package.fromName(pkgName).FunctionList
for i = 1:size(pkgFuncs)
    x = pkgFuncs(i);

    name = x.Name;
    inparams = strjoin(transpose(x.InputNames), ';');
    outparams = strjoin(transpose(x.OutputNames), ';');
    static = sprintf('%d', x.Static); 
    access = x.Access;

    fields = strjoin({name, inparams, outparams, access, static}, '\t');
    functionCsv = strjoin({functionCsv, fields}, '\r');
end
end

