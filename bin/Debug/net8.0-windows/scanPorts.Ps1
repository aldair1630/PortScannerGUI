# Escaneo de puertos abiertos y cierre de procesos no esenciales
# Script mejorado: Manejo de errores robusto, verificación de privilegios, reporte detallado y cierre opcional de procesos en segundo plano no Microsoft/importantes.
# ADVERTENCIA: El cierre de procesos puede causar inestabilidad. Ejecutar como Administrador.

param(
    [string]$ReportPath = "$env:USERPROFILE\Desktop\Reporte_Puertos.txt",
    [ValidateSet("TXT", "CSV", "JSON")]
    [string]$ExportFormat = "TXT",
    [switch]$SaveHistory = $false,
    [switch]$KillNonEssential = $false,
    [switch]$Verbose = $false,
    [string]$TargetIP = "",
    [string]$AlertPorts = "",
    [switch]$UseDB = $false
)

# Función para verificar privilegios de administrador
function Test-Admin {
    $currentUser = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($currentUser)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Verificar privilegios
Write-Host "Script started. Checking admin..."
if (-not (Test-Admin)) {
    Write-Warning "Este script requiere privilegios de Administrador para escanear puertos y cerrar procesos. Ejecuta como Admin."
    exit 1
}
Write-Host "Admin check passed."

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$header = "Reporte de Puertos Escuchando - $timestamp`n`n"

$killReportPath = "$env:USERPROFILE\Desktop\Procesos_Cerrados.txt"
$killHeader = "Procesos No Esenciales Cerrados - $timestamp`n`n"
$killLog = $killHeader

$historyPath = "$env:USERPROFILE\Desktop\history.json"

# Manejo de escaneo remoto si TargetIP proporcionado
$remoteData = @()
if ($TargetIP -and $TargetIP -ne "") {
    Write-Host "Iniciando escaneo remoto en $TargetIP..."
    $commonPorts = 1..1024  # Rango de puertos comunes para escaneo remoto (ajustable)
    foreach ($port in $commonPorts) {
        try {
            $result = Test-NetConnection -ComputerName $TargetIP -Port $port -WarningAction SilentlyContinue -InformationLevel Quiet
            if ($result) {
                $remoteData += [PSCustomObject]@{
                    Protocolo = "TCP Remote"
                    Direccion = "$TargetIP`:$port"
                    PID = "N/A"
                    Proceso = "Remoto"
                    Ruta = "N/A"
                    Compania = "N/A"
                }
                if ($Verbose) { Write-Host "Puerto $port abierto en $TargetIP" }
            }
        } catch {
            if ($Verbose) {
                $errMsg = $_.Exception.Message
                Write-Warning "Error escaneando puerto $port en ${TargetIP}: $errMsg"
            }
        }
    }
}



try {
    # Obtener conexiones activas con manejo de errores
    $connections = @()
    try {
        $connections = Get-NetTCPConnection | Where-Object { $_.State -eq 'Listen' }
        if ($Verbose) { Write-Host "TCP conexiones obtenidas: $($connections.Count)" }
    } catch {
        Write-Warning "Error al obtener conexiones TCP: $($_.Exception.Message)"
    }

    $udpListeners = @()
    try {
        $udpListeners = Get-NetUDPEndpoint
        if ($Verbose) { Write-Host "UDP endpoints obtenidos: $($udpListeners.Count)" }
    } catch {
        Write-Warning "Error al obtener endpoints UDP: $($_.Exception.Message)"
    }

    # Función mejorada para obtener nombre del proceso
    function Get-ProcessInfo($processId) {
        try {
            $proc = Get-Process -Id $processId -ErrorAction Stop
            return @{
                Name = $proc.ProcessName
                Path = $proc.Path
                Company = if ($proc.Company) { $proc.Company } else { "N/A" }
            }
        } catch {
            return @{
                Name = "Desconocido"
                Path = "N/A"
                Company = "N/A"
            }
        }
    }

    # Construir datos para reporte
    $tcpData = $connections | ForEach-Object {
        $processInfo = Get-ProcessInfo $_.OwningProcess
        [PSCustomObject]@{
            Protocolo = "TCP"
            Direccion = "$($_.LocalAddress):$($_.LocalPort)"
            PID = $_.OwningProcess
            Proceso = $processInfo.Name
            Ruta = $processInfo.Path
            Compania = $processInfo.Company
        }
    }

    $udpData = $udpListeners | ForEach-Object {
        $processInfo = Get-ProcessInfo $_.OwningProcess
        [PSCustomObject]@{
            Protocolo = "UDP"
            Direccion = "$($_.LocalAddress):$($_.LocalPort)"
            PID = $_.OwningProcess
            Proceso = $processInfo.Name
            Ruta = $processInfo.Path
            Compania = $processInfo.Company
        }
    }

    $allData = $tcpData + $udpData + $remoteData
    $totalPorts = $allData.Count

    if ($Verbose) {
        Write-Host "TCP conexiones: $($tcpData.Count)"
        Write-Host "UDP endpoints: $($udpData.Count)"
        if ($remoteData.Count -gt 0) { Write-Host "Puertos remotos abiertos: $($remoteData.Count)" }
    }

    # Verificar puertos de alerta si proporcionados
    $alertMessages = @()
    if ($AlertPorts -and $AlertPorts -ne "") {
        $alertPortsList = $AlertPorts -split ',' | ForEach-Object { $_.Trim() }
        foreach ($alertPort in $alertPortsList) {
            # Verificar en datos locales y remotos
            $isOpen = $allData | Where-Object { $_.Direccion -like "*`:$alertPort*" }
            if ($isOpen) {
                $location = if ($isOpen | Where-Object { $_.Protocolo -eq "TCP Remote" }) { "$TargetIP (remoto)" } else { "local" }
                $alertMessages += "ALERTA: Puerto crítico $alertPort abierto en $location"
                Write-Host $alertMessages[-1]
            }
        }
    }

    # Generar contenido según formato
    switch ($ExportFormat) {
        "TXT" {
            $reportContent = $header
            foreach ($item in $allData) {
                $reportContent += "$($item.Protocolo) | Dirección: $($item.Direccion) | PID: $($item.PID) | Proceso: $($item.Proceso) | Ruta: $($item.Ruta) | Compañía: $($item.Compania)`n"
            }
            $reportContent += "`nTotal de puertos: $totalPorts"
            if ($alertMessages.Count -gt 0) {
                $reportContent += "`n`nAlertas:`n"
                foreach ($alert in $alertMessages) {
                    $reportContent += "$alert`n"
                }
            }
        }
        "CSV" {
            $reportContent = "Protocolo,Direccion,PID,Proceso,Ruta,Compania`n"
            foreach ($item in $allData) {
                $reportContent += "`"$($item.Protocolo)`",`"$($item.Direccion)`",`"$($item.PID)`",`"$($item.Proceso)`",`"$($item.Ruta)`",`"$($item.Compania)`"`n"
            }
            $reportContent += "`n# Total de puertos: $totalPorts"
            if ($alertMessages.Count -gt 0) {
                $reportContent += "`n# Alertas:`n"
                foreach ($alert in $alertMessages) {
                    $reportContent += "# $alert`n"
                }
            }
        }
        "JSON" {
            $reportData = @{
                Timestamp = $timestamp
                TotalPorts = $totalPorts
                Connections = $allData
                Alerts = $alertMessages
            }
            $reportContent = $reportData | ConvertTo-Json -Depth 3
        }
    }

    # Guardar reporte
    try {
        $extension = switch ($ExportFormat) { "TXT" { ".txt" } "CSV" { ".csv" } "JSON" { ".json" } }
        $fullPath = if ($ReportPath -match '\.[^.]*$') { $ReportPath } else { $ReportPath + $extension }
        if (Test-Path $fullPath) { Remove-Item $fullPath -Force }
        Set-Content -Path $fullPath -Value $reportContent -Encoding UTF8
        Write-Host "✅ Reporte de puertos generado en: $fullPath (Formato: $ExportFormat)"
    } catch {
        Write-Error "Error al guardar reporte: $($_.Exception.Message)"
    }

    # Guardar en historial si solicitado
    if ($SaveHistory) {
        try {
            $historyEntry = @{
                Timestamp = $timestamp
                TotalPorts = $totalPorts
                ExportFormat = $ExportFormat
                ReportPath = $fullPath
                KillNonEssential = $KillNonEssential.IsPresent
            }
            $history = @()
            if (Test-Path $historyPath) {
                $history = Get-Content $historyPath | ConvertFrom-Json
            }
            $history += $historyEntry
            $history | ConvertTo-Json -Depth 3 | Set-Content -Path $historyPath -Encoding UTF8
            if ($Verbose) { Write-Host "Escaneo guardado en historial: $historyPath" }
        } catch {
            Write-Warning "Error al guardar historial: $($_.Exception.Message)"
        }
    }

    # Sección de cierre de procesos no esenciales (solo si -KillNonEssential)
    if ($KillNonEssential) {
        Write-Host "Iniciando identificación y cierre de procesos en segundo plano no esenciales..."
        $backgroundProcesses = @()
        try {
            $backgroundProcesses = Get-Process | Where-Object { $_.MainWindowTitle -eq "" -and $_.Id -ne 0 }
            if ($Verbose) { Write-Host "Procesos en segundo plano encontrados: $($backgroundProcesses.Count)" }
        } catch {
            Write-Warning "Error al obtener procesos: $($_.Exception.Message)"
        }

        # Procesos importantes a excluir (sistema crítico)
        $importantProcesses = @("explorer", "svchost", "lsass", "winlogon", "csrss", "wininit", "services", "smss", "dwm", "audiodg", "conhost")

        # Filtrar procesos no esenciales: No Microsoft y no importantes
        $nonEssential = $backgroundProcesses | Where-Object {
            $proc = $_
            $isMicrosoft = ($proc.Path -like "*Windows*") -or ($proc.Company -like "*Microsoft*")
            $isImportant = $importantProcesses -contains $proc.ProcessName.ToLower()
            -not ($isMicrosoft -or $isImportant)
        }

        if ($nonEssential.Count -eq 0) {
            Write-Host "No se encontraron procesos no esenciales para cerrar."
            return
        }

        Write-Host "Procesos no esenciales identificados ($($nonEssential.Count)):"
        $nonEssential | ForEach-Object { Write-Host "- $($_.ProcessName) (PID: $($_.Id))" }

        $confirmation = Read-Host "¡ADVERTENCIA! Cerrar estos procesos puede causar problemas. ¿Confirmar cierre? (S/N)"
        if ($confirmation -notmatch "^[Ss]") {
            Write-Host "Cierre cancelado."
            return
        }

        foreach ($proc in $nonEssential) {
            try {
                $procInfo = Get-ProcessInfo $proc.Id
                Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                $killLog += "Cerrado: $($procInfo.Name) | PID: $($proc.Id) | Ruta: $($procInfo.Path) | Razón: No esencial`n"
                Write-Host "✅ Cerrado: $($procInfo.Name) (PID: $($proc.Id))"
            } catch {
                $killLog += "Error al cerrar $($proc.ProcessName) (PID: $($proc.Id)): $($_.Exception.Message)`n"
                Write-Warning "Error al cerrar $($proc.ProcessName): $($_.Exception.Message)"
            }
        }

        # Guardar reporte de procesos cerrados
        if ($killLog -ne $killHeader) {
            if (Test-Path $killReportPath) {
                Remove-Item $killReportPath -Force
            }
            Set-Content -Path $killReportPath -Value $killLog -Encoding UTF8 -ErrorAction Stop
            Write-Host "📄 Reporte de procesos cerrados en: $killReportPath"
        }
    } else {
        Write-Host "Usa -KillNonEssential para cerrar procesos no esenciales."
    }

} catch {
    Write-Error "Error general en el script: $($_.Exception.Message)"
    exit 1
}

Write-Host "Script completado exitosamente."
