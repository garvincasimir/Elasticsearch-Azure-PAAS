
function get_solution_files([String] $pattern){
	$files = $DTE.Solution.Projects |
                   Select-Object -Expand ProjectItems |
                  Where-Object{($_.Name -like $pattern) -and (Test-Path $_.FileNames(0))} |
                   Select -Expand Properties | Where-Object{$_.Name -eq 'FullPath'} |
                   Select -Expand Value
  return $files
}

function add_cscfg_settings([string] $projectName, [string] $templateFile, [string[]] $files){
  [xml] $defaultSettings = gc $templateFile
  $defaults = $defaultSettings.ServiceConfiguration.Role
  foreach ($file in $files) {
    Write-Host Processing $file
		[xml] $config = gc $file

    #config settings
    $role = $config.ServiceConfiguration.Role |
               Where-Object{$_.name -eq $projectName} |
               Select-Object -first 1

    if(!$role){
      Write-Host No matching roles found
    }
    else{
      #Configuration Settings
      Write-Host Creating configuration settings
      if($role.ConfigurationSettings -eq $null){
        $importConfigSettings = $config.ImportNode($defaults.ConfigurationSettings,$true)
        $role.AppendChild($importConfigSettings)
      }

      foreach($item in $defaults.ConfigurationSettings.Setting) {
        Write-Host Adding Setting: $item.name
        $configItem = $role.ConfigurationSettings.Setting.name | Where-Object{$_ -eq $item.name}

        if($configItem -eq $null){
          $importItem = $config.ImportNode($item,$true)
          $role.Item("ConfigurationSettings").AppendChild($importItem)
        }

      }


      Write-Host Saving $file
      $config.Save($file)
    }
  }
}

function add_csdef_settings([string] $projectName, [string] $templateFile, [string[]] $files){
  [xml]$defaultSettings = gc $templateFile
  $defaults = $defaultSettings.ServiceDefinition.WorkerRole
  foreach ($file in $files) {
    Write-Host Processing $file
		[xml] $config = gc $file


    #config settings
    $role = $config.ServiceDefinition.WebRole |
               Where-Object{$_.name -eq $projectName} |
               Select-Object -first 1

    if(!$role){
      Write-Host No WebRole Found attempting WorkerRole
      $role = $config.ServiceDefinition.WorkerRole |
                 Where-Object{$_.name -eq $projectName} |
                 Select-Object -first 1
    }

    if(!$role){
      Write-Host No matching roles found
    }
    else{

    #Runtime Must run elevated
    Write-Host Configuring runtime...
    if($role.Runtime -eq $null){
      $importRuntime = $config.ImportNode($defaults.Runtime,$true)
      $role.AppendChild($importRuntime)
    }
    $role.Runtime.SetAttribute('executionContext','elevated')

      #Endpoints
      Write-Host Creating endpoint...
      if($role.Endpoints -eq $null){
        $importEndpoint = $config.ImportNode($defaults.Endpoints,$true)
        $role.Appendchild($importEndpoint)
      }
      foreach($item in $defaults.Endpoints.Endpoint) {
        Write-Host Adding Endpoint: $item.name
        $endpointItem = $role.Endpoints.Endpoint.name | Where-Object{$_ -eq $item.name}

        if($endpointItem -eq $null){
          $importEndpointItem = $config.ImportNode($item,$true)
          $role.Item("Endpoints").AppendChild($importEndpointItem)
        }
      }

      foreach($item in $defaults.Endpoints.InternalEndpoint) {
        Write-Host Adding Internal Endpoint: $item.name
        $endpointItem = $role.Endpoints.InternalEndpoint.name | Where-Object{$_ -eq $item.name}

        if($endpointItem -eq $null){
          $importEndpointItem = $config.ImportNode($item,$true)
          $role.Item("Endpoints").AppendChild($importEndpointItem)
        }

      }

      #Local Resources
      Write-Host Creating local resources...
      if($role.LocalResources -eq $null){
        $importLocalResources = $config.ImportNode($defaults.LocalResources,$true)
        $role.AppendChild($importLocalResources)
      }
      foreach($item in $defaults.LocalResources.LocalStorage) {
        Write-Host Adding Resource: $item.name
        $LocalStorageItem = $role.LocalResources.LocalStorage.name | Where-Object{$_ -eq $item.name}

        if($LocalStorageItem -eq $null){
          $importLocalStorageItem = $config.ImportNode($item,$true)
          $role.Item("LocalResources").AppendChild($importLocalStorageItem)
        }

      }

      #Configuration Settings
      Write-Host Creating configuration settings
      if($role.ConfigurationSettings -eq $null){
        $importConfigSettings = $config.ImportNode($defaults.ConfigurationSettings,$true)
        $role.AppendChild($importConfigSettings)
      }

      foreach($item in $defaults.ConfigurationSettings.Setting) {
        Write-Host Adding Setting: $item.name
        $configItem = $role.ConfigurationSettings.Setting.name | Where-Object{$_ -eq $item.name}

        if($configItem -eq $null){
          $importItem = $config.ImportNode($item,$true)
          $role.Item("ConfigurationSettings").AppendChild($importItem)
        }

      }

      Write-Host Saving $file
      $config.Save($file)

    }
  }
}

function remove_cscfg_settings([string] $projectName, [string] $templateFile, [string[]] $files){
  [xml] $defaultSettings = gc $templateFile
  $defaults = $defaultSettings.ServiceConfiguration.Role
  foreach ($file in $files) {
    Write-Host Processing $file
		[xml] $config = gc $file

    #config settings
    $role = $config.ServiceConfiguration.Role |
               Where-Object{$_.name -eq $projectName} |
               Select-Object -first 1

    if(!$role){
      Write-Host No matching roles found
    }
    else{
      #Configuration Settings
      Write-Host Removing configuration settings

      foreach($item in $defaults.ConfigurationSettings.Setting | Where-Object{$_.name -ne "StorageConnection"}) {
        Write-Host Removing Setting: $item.name
        $configItem = $role.ConfigurationSettings.Setting | Where-Object{$_.name -eq $item.name}

        if($configItem -ne $null){
          $configItem.ParentNode.RemoveChild($configItem);
        }

      }

      Write-Host Saving $file
      $config.Save($file)
    }
  }
}

function remove_csdef_settings([string] $projectName, [string] $templateFile, [string[]] $files){
  [xml]$defaultSettings = gc $templateFile
  $defaults = $defaultSettings.ServiceDefinition.WorkerRole
  foreach ($file in $files) {
    Write-Host Processing $file
		[xml] $config = gc $file


    #config settings
    $role = $config.ServiceDefinition.WebRole |
               Where-Object{$_.name -eq $projectName} |
               Select-Object -first 1

    if(!$role){
      Write-Host No WebRole Found attempting WorkerRole
      $role = $config.ServiceDefinition.WorkerRole |
                 Where-Object{$_.name -eq $projectName} |
                 Select-Object -first 1
    }

    if(!$role){
      Write-Host No matching roles found
    }
    else{

      #Endpoints
      Write-Host removing endpoint...
      foreach($item in $defaults.Endpoints.Endpoint) {
        Write-Host Removing Endpoint: $item.name
        $endpointItem = $role.Endpoints.Endpoint | Where-Object{$_.name -eq $item.name}

        if($endpointItem -ne $null){
          $endpointItem.ParentNode.RemoveChild($endpointItem)
        }
      }

      foreach($item in $defaults.Endpoints.InternalEndpoint) {
        Write-Host Removing Internal Endpoint: $item.name
        $endpointItem = $role.Endpoints.InternalEndpoint | Where-Object{$_.name -eq $item.name}

        if($endpointItem -ne $null){
          $endpointItem.ParentNode.RemoveChild($endpointItem)
        }

      }

      #Local Resources
      Write-Host Removing local resources...
      foreach($item in $defaults.LocalResources.LocalStorage) {
        Write-Host Removing Resource: $item.name
        $LocalStorageItem = $role.LocalResources.LocalStorage | Where-Object{$_.name -eq $item.name}

        if($LocalStorageItem -ne $null){
          $LocalStorageItem.ParentNode.RemoveChild($LocalStorageItem)
        }

      }

      #Configuration Settings
      Write-Host Removing configuration settings

      foreach($item in $defaults.ConfigurationSettings.Setting) {
        Write-Host Removing Setting: $item.name
        $configItem = $role.ConfigurationSettings.Setting | Where-Object{$_.name -eq $item.name}

        if($configItem -ne $null){
          $configItem.ParentNode.RemoveChild($configItem)
        }

      }

      Write-Host Saving $file
      $config.Save($file)

    }
  }
}
