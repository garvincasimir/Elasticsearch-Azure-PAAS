param($installPath, $toolsPath, $package, $project)

Import-Module (Join-Path $toolsPath library.psm1) -force

$csdefFiles = get_solution_files "ServiceDefinition.csdef"
$csdefTemplate = Join-Path $toolsPath "/templates/ServiceDefinition.template.csdef"
remove_csdef_settings $project.name $csdefTemplate $csdefFiles

$cscfgFiles = get_solution_files "ServiceConfiguration.*.cscfg"
$cscfgTemplate = Join-Path $toolsPath "/templates/ServiceConfiguration.template.cscfg"
remove_cscfg_settings $project.name $cscfgTemplate $cscfgFiles
