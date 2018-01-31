# Taken from psake https://github.com/psake/psake and modified
<#  
.SYNOPSIS
  This is a helper function that runs a scriptblock and checks the PS variable $lastexitcode
  to see if an error occcured. If an error is detected then an exception is thrown.
  This function allows you to run command-line programs without having to
  explicitly check the $lastexitcode variable.
.EXAMPLE
  exec { svn info $repository_trunk } "Error executing SVN. Please verify SVN command-line client is installed"
#>
function Exec  
{
    [CmdletBinding()]
    param(
        [Parameter(Position=0,Mandatory=1)][scriptblock]$cmd,
        [Parameter(Position=1,Mandatory=0)][string]$errorMessage = ($msgs.error_bad_command -f $cmd)
    )
    & $cmd
    if ($lastexitcode -ne 0)
    {
        throw ("Exec: " + $errorMessage)
    }
}

exec { & dotnet restore }
exec { & dotnet clean }
exec { & dotnet build --configuration Release }
exec { & dotnet test --no-build --no-restore --configuration Release Winton.Extensions.Threading.Actor.Tests.Unit\Winton.Extensions.Threading.Actor.Tests.Unit.csproj }
exec { & dotnet pack --no-build --no-restore Winton.Extensions.Threading.Actor\Winton.Extensions.Threading.Actor.csproj --configuration Release }