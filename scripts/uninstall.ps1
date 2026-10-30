#Requires -Version 5.1
<#
.SYNOPSIS
    Uninstall the tbc-acad-mcp plugin bundle (TbcAcadMcp.bundle).
#>
[CmdletBinding(SupportsShouldProcess)]
param()

& "$PSScriptRoot\install.ps1" -Uninstall @PSBoundParameters
