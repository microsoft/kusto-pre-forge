# ARM Templates

This section contains different ARM templates offering you different level of control on the deployment.

Each template was authored in [BICEP](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/).  The corresponding JSON version was generated using `az bicep build -f <bicep template>`.

## Complete sample

This is the simplest template available on the root page.  It requires no parameters as it generates names for every resources with `kpf` as prefix and a unique suffix.

[Bicep template](complete-sample.bicep) / [JSON template](complete-sample.json)

[![Deploy button](http://azuredeploy.net/deploybutton.png)](https://portal.azure.com/#create/Microsoft.Template/uri/https:%2F%2Fraw.githubusercontent.com%2Fmicrosoft%2Fkusto-pre-forge%2Fmain%2Ftemplates%2Fcomplete-sample.json)

## Complete with names

This template deploys the entire solution but require you to provide the name of each resource.

[Bicep template](complete-with-names.bicep) / [JSON template](complete-with-names.json)

[![Deploy button](http://azuredeploy.net/deploybutton.png)](https://portal.azure.com/#create/Microsoft.Template/uri/https:%2F%2Fraw.githubusercontent.com%2Fmicrosoft%2Fkusto-pre-forge%2Fmain%2Ftemplates%2Fcomplete-with-names.json)

