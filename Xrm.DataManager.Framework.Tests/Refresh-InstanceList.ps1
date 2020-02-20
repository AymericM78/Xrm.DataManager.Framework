param
(
    $region = "EMEA", # NorthAmerica, EMEA, APAC, SouthAmerica, Oceania, JPN, CAN, IND, and NorthAmerica2
    $authType = "Office365" # AD, IDF, oAuth, Office365
)

Clear-Host;

$ErrorActionPreference = "Stop";
$DebugPreference = "Continue";

$installerUrl = "https://raw.githubusercontent.com/AymericM78/D365-PowerShell/master/D365.DevOps.Powershell.Installer.ps1";
$targetPath = "$($env:temp)\D365.DevOps.Powershell.Installer.ps1";
Invoke-WebRequest -Uri $installerUrl -OutFile $targetPath;

. $targetPath;

# Read configuration file to retrieve login and password
$configFilePath = "$PSScriptRoot\App.config";
$configXml = [xml] [system.IO.File]::ReadAllText($configFilePath);
$login  = ($configXml.configuration.appSettings.add | Where-object { $_.Key -eq "Crm.User.Name" }).value;
$password  = ($configXml.configuration.appSettings.add | Where-object { $_.Key -eq "Crm.User.Password" }).value;
# Load PowerShell Framework
. "${env:D365.DevOps.Powershell.Path}\CrmDevOps.Tools.Common\Common.ps1";

# Choose instance
$instances = (. "${env:D365.DevOps.Powershell.Path}\CrmDevOps.Tools.Common\Instances\Get-Instances.ps1" -login $login -password $password);
$instances = $instances | Sort-Object -Property FriendlyName;

Write-Host "Replacing file content..." -NoNewline -ForegroundColor Gray;

$targetFilePath = "$PsScriptRoot\Instances.xml";

$output = New-Object "System.Text.StringBuilder";
$output.AppendLine("<?xml version=`"1.0`" encoding=`"utf-8`"?>") | Out-Null;
$output.AppendLine("<Instances>") | Out-Null;
foreach($instance in $instances)
{
    # Extract instance name from url
    $instanceName = $instance.Url;
    $stopIndex = $instanceName.IndexOf(".");
    $instanceName = $instanceName.Remove($stopIndex);    
    $startIndex = $instanceName.LastIndexOf("/") + 1;
    $instanceName = $instanceName.Substring($startIndex, ($instanceName.Length - $startIndex));
    
    $instanceLineContent = [string]::Concat("<Instance Name=`"", $instanceName, 
        "`" UniqueName=`"",$instance.UniqueName, 
        "`" DisplayName=`"", $instance.FriendlyName, 
        "`" Url=`"",$instance.OrganizationUrl, 
        "`" Login=`"",$login, 
        "`" Password=`"",$password,
        "`" />");

    $output.AppendLine($instanceLineContent) | Out-Null;
}
$output.AppendLine("</Instances>") | Out-Null;

$output.ToString() | Out-File -FilePath $targetFilePath -Encoding utf8 -Force;

Write-Host "[OK]" -ForegroundColor Green;