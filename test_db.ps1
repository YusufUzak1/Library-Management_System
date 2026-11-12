$dll = (Get-ChildItem -Path '.' -Recurse -Filter 'MySqlConnector.dll' | Select-Object -ExpandProperty FullName -First 1)
Write-Host "Found DLL: $dll"
Add-Type -Path $dll

$candidates = @(
    @{ User = "root"; Pass = "RootPassword123!" },
    @{ User = "root"; Pass = "" },
    @{ User = "root"; Pass = "root" },
    @{ User = "root"; Pass = "123456" },
    @{ User = "chat_user"; Pass = "ChatPassword123!" },
    @{ User = "kutuphane"; Pass = "kutuphane" }
)

foreach ($c in $candidates) {
    $u = $c.User
    $p = $c.Pass
    $cs = "Server=127.0.0.1;Port=3306;User ID=$u;Password=$p;ConnectionTimeout=2"
    try {
        $conn = New-Object MySqlConnector.MySqlConnection($cs)
        $conn.Open()
        Write-Host "CONNECTED SUCCESSFULLY with User='$u', Pass='$p'!"
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SHOW DATABASES;"
        $r = $cmd.ExecuteReader()
        Write-Host "Databases:"
        while ($r.Read()) {
            Write-Host " - " $r.GetString(0)
        }
        $r.Close()
        $conn.Close()
        exit 0
    } catch {
        Write-Host "Failed with User='$u', Pass='$p': $($_.Exception.Message)"
    }
}

