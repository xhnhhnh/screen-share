#requires -version 5.1
<#
.SYNOPSIS
自动配置两台 Windows 电脑之间的雷电/USB4 点对点网络。

.DESCRIPTION
请在三分钟内于两台电脑上运行同一个脚本。脚本会通过现有的 USB4
链路本地网络互相发现，完成双向握手，根据网卡 MAC 地址自动决定
两端地址，然后配置 192.168.250.1/30 和 192.168.250.2/30。

如果等待超时，脚本不会写入静态 IP。本脚本不会创建 Windows 二层
[网络桥]，也不会修改 Wi-Fi、普通以太网、Hyper-V 或 VMware 网卡。

.PARAMETER AdapterName
可选。当系统存在多个 USB4 P2P 网卡时，用它指定网卡名称。

.PARAMETER EnableFileSharing
允许对端通过这张网卡访问 SMB TCP/445。需要 Windows 文件共享时，
请在两台电脑上都使用此开关。

.PARAMETER WaitSeconds
等待另一台电脑的秒数，默认 180 秒。

.PARAMETER DoubleClick
兼容参数。由其他启动器调用并希望脚本自行保留窗口时使用。

.EXAMPLE
powershell.exe -ExecutionPolicy Bypass -File .\Enable-ThunderboltBridge.ps1

.EXAMPLE
powershell.exe -ExecutionPolicy Bypass -File .\Enable-ThunderboltBridge.ps1 -EnableFileSharing
#>

[CmdletBinding()]
param(
    [string]$AdapterName,

    [switch]$EnableFileSharing,

    [ValidateRange(30, 600)]
    [int]$WaitSeconds = 180,

    [switch]$DoubleClick
)

$ErrorActionPreference = 'Stop'

try {
    [Console]::InputEncoding = New-Object Text.UTF8Encoding($false)
    [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
}
catch {
    # 某些非控制台宿主不允许调整编码，不影响核心配置。
}

try {
    $Host.UI.RawUI.WindowTitle = '雷电 / USB4 网桥自动配置'
}
catch {
    # 非控制台宿主可能没有窗口标题。
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Quote-ProcessArgument {
    param([Parameter(Mandatory = $true)][string]$Value)
    return '"' + $Value.Replace('"', '\"') + '"'
}

if (-not (Test-IsAdministrator)) {
    if (-not $PSCommandPath) {
        throw '请先将此脚本保存为 .ps1 文件后再运行。'
    }

    $elevationArguments = @(
        '-NoProfile'
        '-ExecutionPolicy'
        'Bypass'
        '-File'
        (Quote-ProcessArgument -Value $PSCommandPath)
        '-WaitSeconds'
        $WaitSeconds
    )

    if ($AdapterName) {
        $elevationArguments += '-AdapterName'
        $elevationArguments += (Quote-ProcessArgument -Value $AdapterName)
    }
    if ($EnableFileSharing) {
        $elevationArguments += '-EnableFileSharing'
    }
    if ($DoubleClick) {
        $elevationArguments += '-DoubleClick'
    }

    Write-Host '正在请求管理员权限，请在弹出的窗口中选择 [是]……' -ForegroundColor Cyan
    try {
        $powerShellExe = Join-Path $PSHOME 'powershell.exe'
        $elevatedProcess = Start-Process `
            -FilePath $powerShellExe `
            -Verb RunAs `
            -ArgumentList $elevationArguments `
            -Wait `
            -PassThru
    }
    catch {
        Write-Host ''
        Write-Host '未获得管理员权限，配置尚未开始。' -ForegroundColor Red
        Write-Host '请重新双击启动器，并在用户账户控制窗口中选择 [是]。'
        if ($DoubleClick) {
            [void](Read-Host '按 Enter 键关闭窗口')
        }
        exit 1
    }
    exit $elevatedProcess.ExitCode
}

$mutexCreated = $false
$mutex = New-Object System.Threading.Mutex($true, 'Global\ThunderboltUSB4P2PAutoConfig', [ref]$mutexCreated)
if (-not $mutexCreated) {
    $mutex.Dispose()
    Write-Host '这台电脑上已有一个配置窗口正在运行，请不要重复启动。' -ForegroundColor Red
    if ($DoubleClick) {
        [void](Read-Host '按 Enter 键关闭窗口')
    }
    exit 1
}

try {
    $prefixLength = 30
    $discoveryPort = 45194
    $discoveryMagic = 'ThunderboltUSB4P2P-AutoConfig-v2'
$firewallGroup = '雷电 USB4 点对点网络'

    Write-Host '============================================================' -ForegroundColor DarkCyan
    Write-Host '           雷电 / USB4 网桥自动配置工具' -ForegroundColor Cyan
    Write-Host '============================================================' -ForegroundColor DarkCyan
    Write-Host ''
    Write-Host '请在另一台电脑上也双击运行同一个启动器。'
    Write-Host "两台电脑需要在 $WaitSeconds 秒内同时进入等待状态。"
    Write-Host ''
    Write-Host '[1/7] 正在查找雷电/USB4 点对点网卡……'

    if ($AdapterName) {
        $adapter = Get-NetAdapter -Name $AdapterName -IncludeHidden -ErrorAction Stop
        if ($adapter.InterfaceDescription -notmatch '(?i)(USB4.*P2P.*Network|P2P.*USB4.*Network)') {
            throw "网卡 [$AdapterName] 不是 USB4 P2P Network Adapter。为避免误改，操作已停止。"
        }
    }
    else {
        $matches = @(Get-NetAdapter -IncludeHidden | Where-Object {
            $_.InterfaceDescription -match '(?i)(USB4.*P2P.*Network|P2P.*USB4.*Network)'
        })

        if ($matches.Count -eq 0) {
            throw '未找到 USB4 P2P Network Adapter。请检查雷电线、USB4/雷电授权、Windows 更新和设备管理器。'
        }
        if ($matches.Count -gt 1) {
            $candidateNames = ($matches | ForEach-Object { "'$($_.Name)' ($($_.Status))" }) -join ', '
            throw "发现多个 USB4 P2P 网卡：$candidateNames。请使用 -AdapterName <网卡名称> 指定目标网卡后重试。"
        }
        $adapter = $matches[0]
    }

    Write-Host "      已找到：$($adapter.Name) / $($adapter.InterfaceDescription)" -ForegroundColor Green

    Write-Host '[2/7] 正在启用网卡并等待物理链路……'
    if ($adapter.Status -eq 'Disabled') {
        Enable-NetAdapter -Name $adapter.Name -Confirm:$false
    }
    Enable-NetAdapterBinding -Name $adapter.Name -ComponentID 'ms_tcpip' -ErrorAction Stop | Out-Null

    $linkDeadline = (Get-Date).AddSeconds(20)
    do {
        Start-Sleep -Milliseconds 500
        $adapter = Get-NetAdapter -Name $adapter.Name
    } while ($adapter.Status -ne 'Up' -and (Get-Date) -lt $linkDeadline)

    if ($adapter.Status -ne 'Up') {
        throw "USB4 P2P 网卡当前状态为 [$($adapter.Status)]，尚未连通。请确认两台电脑已用雷电/USB4 线连接，然后重试。"
    }

    $localAddressDeadline = (Get-Date).AddSeconds(20)
    $discoveryLocalAddress = $null
    do {
        $discoveryLocalAddress = Get-NetIPAddress `
            -InterfaceIndex $adapter.ifIndex `
            -AddressFamily IPv4 `
            -ErrorAction SilentlyContinue | Where-Object {
                $_.AddressState -ne 'Duplicate' -and
                ($_.IPAddress -like '169.254.*' -or $_.IPAddress -in @('192.168.250.1', '192.168.250.2'))
            } | Sort-Object @{ Expression = { if ($_.IPAddress -like '169.254.*') { 0 } else { 1 } } } |
            Select-Object -First 1
        if (-not $discoveryLocalAddress) { Start-Sleep -Milliseconds 500 }
    } while (-not $discoveryLocalAddress -and (Get-Date) -lt $localAddressDeadline)

    if (-not $discoveryLocalAddress) {
        throw 'USB4 网卡没有可用于发现对端的 IPv4 地址。请重新插拔线缆后重试。'
    }

    $nodeId = ($adapter.MacAddress -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
    if (-not $nodeId) {
        throw 'USB4 P2P 网卡没有可用的 MAC 地址，无法安全地自动分配两端 IP。'
    }

    Write-Host "[3/7] 正在等待另一台电脑，最多等待 $WaitSeconds 秒……"
    Write-Host "      当前发现地址：$($discoveryLocalAddress.IPAddress)"

    $discoveryRuleName = "Thunderbolt-USB4-P2P-Discovery-$PID"
    $udp = $null
    $peerNodeId = $null
    $peerAcknowledged = $false
    $mutualHandshakeAt = $null
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $discoveryDeadline = (Get-Date).AddSeconds($WaitSeconds)
    $handshakeDeadline = $null

    try {
        New-NetFirewallRule `
            -Name $discoveryRuleName `
            -DisplayName '雷电/USB4 点对点网络 - 临时自动发现' `
            -Group $firewallGroup `
            -Direction Inbound `
            -Action Allow `
            -Enabled True `
            -Profile Any `
            -InterfaceAlias $adapter.Name `
            -RemoteAddress Any `
            -Protocol UDP `
            -LocalPort $discoveryPort | Out-Null

        $udp = New-Object System.Net.Sockets.UdpClient
        $udp.ExclusiveAddressUse = $false
        $udp.Client.SetSocketOption(
            [Net.Sockets.SocketOptionLevel]::Socket,
            [Net.Sockets.SocketOptionName]::ReuseAddress,
            $true
        )
        $udp.Client.Bind((New-Object Net.IPEndPoint([Net.IPAddress]::Parse($discoveryLocalAddress.IPAddress), $discoveryPort)))
        $udp.EnableBroadcast = $true
        $udp.Client.ReceiveTimeout = 150

        $broadcastEndpoint = New-Object Net.IPEndPoint([Net.IPAddress]::Parse('255.255.255.255'), $discoveryPort)
        $remoteEndpoint = New-Object Net.IPEndPoint([Net.IPAddress]::Any, 0)

        while ($true) {
            $now = Get-Date
            if ($now -ge $discoveryDeadline -and
                (-not $handshakeDeadline -or $now -ge $handshakeDeadline)) {
                break
            }

            $message = [ordered]@{
                Magic  = $discoveryMagic
                NodeId = $nodeId
                PeerId = $peerNodeId
            } | ConvertTo-Json -Compress
            $bytes = [Text.Encoding]::UTF8.GetBytes($message)
            [void]$udp.Send($bytes, $bytes.Length, $broadcastEndpoint)

            # Windows 会把受限广播回送给发送端。一次多读取几个排队报文，
            # 避免本机回送报文遮住对端报文。
            for ($receiveCount = 0; $receiveCount -lt 8; $receiveCount++) {
                try {
                    $receivedBytes = $udp.Receive([ref]$remoteEndpoint)
                    $incoming = [Text.Encoding]::UTF8.GetString($receivedBytes) | ConvertFrom-Json -ErrorAction Stop

                    if ($incoming.Magic -eq $discoveryMagic -and $incoming.NodeId -and $incoming.NodeId -ne $nodeId) {
                        if (-not $peerNodeId) {
                            $peerNodeId = [string]$incoming.NodeId
                            $handshakeDeadline = (Get-Date).AddSeconds(15)
                            Write-Host "      已发现另一台电脑：$($remoteEndpoint.Address)，正在确认双向连接……" -ForegroundColor Cyan
                        }
                        elseif ($peerNodeId -ne [string]$incoming.NodeId) {
                            throw '这条 USB4 链路上有多个对端响应。请断开多余连接后重试。'
                        }

                        if ([string]$incoming.PeerId -eq $nodeId) {
                            $peerAcknowledged = $true
                        }
                    }
                }
                catch {
                    $socketException = $_.Exception
                    if ($socketException.InnerException -is [Net.Sockets.SocketException]) {
                        $socketException = $socketException.InnerException
                    }
                    if ($socketException -is [Net.Sockets.SocketException] -and
                        $socketException.SocketErrorCode -eq [Net.Sockets.SocketError]::TimedOut) {
                        break
                    }
                    throw
                }
            }

            if ($peerNodeId -and $peerAcknowledged) {
                if (-not $mutualHandshakeAt) {
                    $mutualHandshakeAt = Get-Date
                    Write-Host '      双向握手成功，双方即将同步开始配置……' -ForegroundColor Green
                }
                elseif (((Get-Date) - $mutualHandshakeAt).TotalSeconds -ge 3) {
                    break
                }
            }

            $remaining = [Math]::Max(0, [Math]::Ceiling($WaitSeconds - $stopwatch.Elapsed.TotalSeconds))
            Write-Progress `
                -Activity '正在等待另一台雷电/USB4 电脑' `
                -Status "剩余 $remaining 秒" `
                -PercentComplete ([Math]::Min(100, 100 * $stopwatch.Elapsed.TotalSeconds / $WaitSeconds))
        }

        Write-Progress -Activity '正在等待另一台雷电/USB4 电脑' -Completed
    }
    finally {
        if ($udp) { $udp.Close() }
        Get-NetFirewallRule -Name $discoveryRuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    }

    if (-not ($peerNodeId -and $peerAcknowledged -and $mutualHandshakeAt)) {
        throw "在 $WaitSeconds 秒内没有确认另一台电脑。脚本没有写入静态 IP。请在两台电脑上于同一个三分钟窗口内重新双击启动器。"
    }

    $comparison = [StringComparer]::OrdinalIgnoreCase.Compare($nodeId, $peerNodeId)
    if ($comparison -eq 0) {
        throw '两张网卡报告了相同的 MAC 地址，无法安全地自动决定两端 IP。'
    }

    if ($comparison -lt 0) {
        $localIp = '192.168.250.1'
        $peerIp = '192.168.250.2'
    }
    else {
        $localIp = '192.168.250.2'
        $peerIp = '192.168.250.1'
    }

    Write-Host '[4/7] 正在检查 IP 地址冲突……'
    $conflicts = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {
        $_.InterfaceIndex -ne $adapter.ifIndex -and
        ($_.IPAddress -eq $localIp -or $_.IPAddress -eq $peerIp -or $_.IPAddress -like '192.168.250.*')
    })
    if ($conflicts.Count -gt 0) {
        $details = ($conflicts | ForEach-Object { "$($_.InterfaceAlias)=$($_.IPAddress)" }) -join ', '
        throw "默认网段 192.168.250.0/30 与本机其他网卡冲突：$details"
    }

    Write-Host "[5/7] 正在配置本机地址 $localIp/$prefixLength（对端：$peerIp）……"

    # 只移除本脚本曾为另一端角色配置的旧地址，不动其他地址。
    $staleManagedIp = Get-NetIPAddress `
        -InterfaceIndex $adapter.ifIndex `
        -AddressFamily IPv4 `
        -IPAddress $peerIp `
        -ErrorAction SilentlyContinue
    if ($staleManagedIp) {
        $staleManagedIp | Remove-NetIPAddress -Confirm:$false
    }

    $existingLocal = Get-NetIPAddress `
        -InterfaceIndex $adapter.ifIndex `
        -AddressFamily IPv4 `
        -IPAddress $localIp `
        -ErrorAction SilentlyContinue
    if (-not $existingLocal) {
        New-NetIPAddress `
            -InterfaceIndex $adapter.ifIndex `
            -IPAddress $localIp `
            -PrefixLength $prefixLength `
            -AddressFamily IPv4 | Out-Null
    }

    Set-NetIPInterface `
        -InterfaceIndex $adapter.ifIndex `
        -AddressFamily IPv4 `
        -Dhcp Disabled `
        -InterfaceMetric 5

    Write-Host '[6/7] 正在设为专用网络并应用仅限对端的防火墙规则……'
    $profileDeadline = (Get-Date).AddSeconds(15)
    $profile = $null
    do {
        $profile = Get-NetConnectionProfile -InterfaceIndex $adapter.ifIndex -ErrorAction SilentlyContinue
        if (-not $profile) { Start-Sleep -Milliseconds 500 }
    } while (-not $profile -and (Get-Date) -lt $profileDeadline)

    if ($profile) {
        Set-NetConnectionProfile -InterfaceIndex $adapter.ifIndex -NetworkCategory Private
    }
    else {
        Write-Warning 'Windows 尚未创建此连接的网络配置文件。IP 已配置，但暂时无法将网络类别设为 [专用]。'
    }

    $icmpRuleName = 'Thunderbolt-USB4-P2P-ICMPv4'
    Get-NetFirewallRule -Name $icmpRuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule `
        -Name $icmpRuleName `
        -DisplayName '雷电/USB4 点对点网络 - 允许对端 Ping' `
        -Group $firewallGroup `
        -Direction Inbound `
        -Action Allow `
        -Enabled True `
        -Profile Private `
        -InterfaceAlias $adapter.Name `
        -RemoteAddress $peerIp `
        -Protocol ICMPv4 `
        -IcmpType 8 | Out-Null

    $smbRuleName = 'Thunderbolt-USB4-P2P-SMB'
    Get-NetFirewallRule -Name $smbRuleName -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    if ($EnableFileSharing) {
        Enable-NetAdapterBinding -Name $adapter.Name -ComponentID 'ms_msclient' -ErrorAction Stop | Out-Null
        Enable-NetAdapterBinding -Name $adapter.Name -ComponentID 'ms_server' -ErrorAction Stop | Out-Null
        New-NetFirewallRule `
            -Name $smbRuleName `
            -DisplayName '雷电/USB4 点对点网络 - 允许对端文件共享' `
            -Group $firewallGroup `
            -Direction Inbound `
            -Action Allow `
            -Enabled True `
            -Profile Private `
            -InterfaceAlias $adapter.Name `
            -RemoteAddress $peerIp `
            -Protocol TCP `
            -LocalPort 445 | Out-Null
    }

    Write-Host '[7/7] 正在验证配置结果……'
    $adapter = Get-NetAdapter -Name $adapter.Name
    $profile = Get-NetConnectionProfile -InterfaceIndex $adapter.ifIndex -ErrorAction SilentlyContinue
    $address = Get-NetIPAddress `
        -InterfaceIndex $adapter.ifIndex `
        -AddressFamily IPv4 `
        -IPAddress $localIp `
        -ErrorAction SilentlyContinue

    $linkStatusText = if ($adapter.Status -eq 'Up') { '已连接' } else { [string]$adapter.Status }
    $networkTypeText = if (-not $profile) {
        '等待 Windows 创建配置文件'
    }
    elseif ($profile.NetworkCategory -eq 'Private') {
        '专用网络'
    }
    else {
        [string]$profile.NetworkCategory
    }
    $fileSharingText = if ($EnableFileSharing) { '已允许当前对端访问 TCP/445' } else { '未开启（安全默认值）' }

    Write-Host ''
    Write-Host '============================================================' -ForegroundColor DarkGreen
    Write-Host '配置完成：雷电 / USB4 点对点网络已经建立' -ForegroundColor Green
    Write-Host '============================================================' -ForegroundColor DarkGreen
    Write-Host "  网卡名称：$($adapter.Name)"
    Write-Host "  链路状态：$linkStatusText"
    Write-Host "  链路速率：$($adapter.LinkSpeed)"
    Write-Host "  本机地址：$($address.IPAddress)/$prefixLength"
    Write-Host "  对端地址：$peerIp"
    Write-Host "  网络类型：$networkTypeText"
    Write-Host "  文件共享：$fileSharingText"
    Write-Host ''
    Write-Host "手动测试命令：ping.exe $peerIp"
    if ($EnableFileSharing) {
        Write-Host "资源管理器访问地址：\\$peerIp"
    }

    Start-Sleep -Seconds 2
    if (Test-Connection -ComputerName $peerIp -Count 2 -Quiet -ErrorAction SilentlyContinue) {
        Write-Host '连通性测试：成功，已经收到另一台电脑的响应。' -ForegroundColor Green
    }
    else {
        Write-Warning '暂时没有收到对端 Ping 响应。请等待几秒，再运行上面显示的测试命令。'
    }
}
catch {
    $scriptExitCode = 1
    Write-Progress -Activity '正在等待另一台雷电/USB4 电脑' -Completed -ErrorAction SilentlyContinue
    Write-Host ''
    Write-Host '============================================================' -ForegroundColor DarkRed
    Write-Host '配置未完成' -ForegroundColor Red
    Write-Host '============================================================' -ForegroundColor DarkRed
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ''
    Write-Host '本次操作已停止。请按上面的提示检查后，在两台电脑上重新双击运行。'
}
finally {
    if ($mutexCreated) {
        $mutex.ReleaseMutex()
    }
    $mutex.Dispose()
}

if (-not $scriptExitCode) {
    $scriptExitCode = 0
}

if ($DoubleClick) {
    Write-Host ''
    [void](Read-Host '按 Enter 键关闭此窗口')
}

exit $scriptExitCode
