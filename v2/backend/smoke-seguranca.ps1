# ============================================================================
# Teste de fumaca da API de seguranca do BSFM (compativel com Windows PowerShell 5.1).
# Exercita os endpoints reais por HTTP, nao as classes.
#
# Uso:
#   powershell -File smoke-seguranca.ps1                 # fase 1
#   powershell -File smoke-seguranca.ps1 -Codigo 123456  # fase 2 (apos ler o codigo)
# ============================================================================
param(
    [string]$Codigo,
    [string]$BaseUrl = 'http://127.0.0.1:5099',
    [string]$Email = 'ana@bsfm.dev',
    [string]$Senha = 'SenhaForte#2026',
    [string]$HmacSecret = 'dev-hmac-secret-com-no-minimo-32-bytes-aqui!!'
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$script:passou = 0
$script:falhou = 0

function Verificar([string]$nome, $condicao, [string]$detalhe = '') {
    if ([bool]$condicao) { $script:passou++; Write-Host "  PASS  $nome" }
    else { $script:falhou++; Write-Host "  FAIL  $nome  -> $detalhe" -ForegroundColor Red }
}

function Hex([byte[]]$bytes) { return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '') }

function Http([string]$metodo, [string]$caminho, [string]$corpo = '', $headers = @{}) {
    $params = @{
        Uri             = "$BaseUrl$caminho"
        Method          = $metodo
        Headers         = $headers
        ContentType     = 'application/json; charset=utf-8'
        UseBasicParsing = $true
        TimeoutSec      = 20
    }
    if ($corpo) { $params['Body'] = [System.Text.Encoding]::UTF8.GetBytes($corpo) }

    try {
        $r = Invoke-WebRequest @params
        return @{ Status = [int]$r.StatusCode; Corpo = [string]$r.Content }
    }
    catch {
        $resp = $_.Exception.Response
        if ($resp -ne $null) {
            $stream = $resp.GetResponseStream()
            $leitor = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8)
            $texto = $leitor.ReadToEnd()
            $leitor.Dispose()
            return @{ Status = [int]$resp.StatusCode; Corpo = $texto }
        }
        return @{ Status = 0; Corpo = $_.Exception.Message }
    }
}

function Assinar([string]$metodo, [string]$caminho, [string]$corpo) {
    $ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $nonce = [guid]::NewGuid().ToString('N')
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $corpoHash = Hex ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($corpo)))
    $sha.Dispose()

    $canonica = "$($metodo.ToUpperInvariant())`n$caminho`n$ts`n$nonce`n$corpoHash"
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($HmacSecret)
    $assinatura = Hex ($hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($canonica)))
    $hmac.Dispose()

    return @{ Timestamp = "$ts"; Nonce = $nonce; Signature = $assinatura }
}

function Json([string]$texto) {
    if ([string]::IsNullOrWhiteSpace($texto)) { return $null }
    try { return $texto | ConvertFrom-Json } catch { return $null }
}

Write-Host ""
Write-Host "[0] Servico no ar"
$saude = Http 'GET' '/saude'
Verificar 'GET /saude responde 200' ($saude.Status -eq 200) "$($saude.Status) $($saude.Corpo)"

if ($Codigo) {
    Write-Host ""
    Write-Host "[6] Redefinicao com codigo valido e novo login"
    $corpoReset = "{`"email`":`"$Email`",`"codigo`":`"$Codigo`",`"novaSenha`":`"NovaSenha#2026`"}"
    $reset = Http 'POST' '/api/auth/redefinir-senha' $corpoReset
    Verificar 'redefinir-senha com codigo correto retorna 200' ($reset.Status -eq 200) "$($reset.Status) $($reset.Corpo)"

    $loginNovo = Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"NovaSenha#2026`"}"
    Verificar 'login com a senha nova funciona' ($loginNovo.Status -eq 200) "$($loginNovo.Status)"
    $dn = Json $loginNovo.Corpo
    Verificar 'resposta traz token' (-not [string]::IsNullOrEmpty($dn.token))

    $loginAntigo = Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"$Senha`"}"
    Verificar 'login com a senha antiga falha' ($loginAntigo.Status -eq 401) "$($loginAntigo.Status)"

    $reuso = Http 'POST' '/api/auth/refresh' "{`"refreshToken`":`"$script:refreshUsado`"}"
    Verificar 'refresh revogado pela troca de senha nao funciona' ($reuso.Status -eq 401) "$($reuso.Status)"
}
else {
    Write-Host ""
    Write-Host "[1] Login"
    $ruim = Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"errada`"}"
    Verificar 'senha errada retorna 401' ($ruim.Status -eq 401) "$($ruim.Status)"
    Verificar 'erro nao revela se o e-mail existe' ((Json $ruim.Corpo).erro -eq 'credenciais_invalidas') "$($ruim.Corpo)"

    $inexistente = Http 'POST' '/api/auth/login' '{"email":"ninguem@bsfm.dev","senha":"qualquer"}'
    Verificar 'e-mail inexistente devolve o MESMO erro' ((Json $inexistente.Corpo).erro -eq 'credenciais_invalidas') "$($inexistente.Corpo)"

    $login = Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"$Senha`"}"
    $dados = Json $login.Corpo
    Verificar 'login valido retorna 200' ($login.Status -eq 200) "$($login.Status) $($login.Corpo)"
    Verificar 'token emitido' (-not [string]::IsNullOrEmpty($dados.token))
    Verificar 'refresh emitido' (-not [string]::IsNullOrEmpty($dados.refreshToken))
    Verificar 'senha nunca aparece na resposta' (-not $login.Corpo.Contains($Senha))

    $script:token = $dados.token
    $script:refresh = $dados.refreshToken
    $script:refreshUsado = $dados.refreshToken

    Write-Host ""
    Write-Host "[2] Perfil sem autenticacao"
    $semToken = Http 'GET' '/api/eu/perfil'
    Verificar 'sem token retorna 401' ($semToken.Status -eq 401) "$($semToken.Status)"

    $tokenFalso = Http 'GET' '/api/eu/perfil' '' @{ Authorization = 'Bearer abc.def.ghi' }
    Verificar 'token forjado retorna 401' ($tokenFalso.Status -eq 401) "$($tokenFalso.Status)"

    $soToken = Http 'GET' '/api/eu/perfil' '' @{ Authorization = "Bearer $($script:token)" }
    Verificar 'token valido sem assinatura retorna 401' ($soToken.Status -eq 401) "$($soToken.Status)"
    Verificar 'erro explicita assinatura ausente' ((Json $soToken.Corpo).erro -eq 'assinatura_ausente') "$($soToken.Corpo)"

    Write-Host ""
    Write-Host "[3] Perfil com sessao + assinatura"
    $s = Assinar 'GET' '/api/eu/perfil' ''
    $ok = Http 'GET' '/api/eu/perfil' '' @{
        Authorization     = "Bearer $($script:token)"
        'X-BSFM-Timestamp' = $s.Timestamp
        'X-BSFM-Nonce'     = $s.Nonce
        'X-BSFM-Signature' = $s.Signature
    }
    $perfil = Json $ok.Corpo
    Verificar 'requisicao assinada retorna 200' ($ok.Status -eq 200) "$($ok.Status) $($ok.Corpo)"
    Verificar 'identidade vem do token (id=1)' ($perfil.id -eq 1) "$($perfil.id)"
    Verificar 'jti devolvido para auditoria' (-not [string]::IsNullOrEmpty($perfil.jti))

    $s2 = Assinar 'GET' '/api/eu/outra-coisa' ''
    $rotaErrada = Http 'GET' '/api/eu/perfil' '' @{
        Authorization     = "Bearer $($script:token)"
        'X-BSFM-Timestamp' = $s2.Timestamp
        'X-BSFM-Nonce'     = $s2.Nonce
        'X-BSFM-Signature' = $s2.Signature
    }
    Verificar 'assinatura de outra rota e rejeitada' ($rotaErrada.Status -eq 401) "$($rotaErrada.Status)"

    $replay = Http 'GET' '/api/eu/perfil' '' @{
        Authorization     = "Bearer $($script:token)"
        'X-BSFM-Timestamp' = $s.Timestamp
        'X-BSFM-Nonce'     = $s.Nonce
        'X-BSFM-Signature' = $s.Signature
    }
    Verificar 'replay do mesmo nonce e rejeitado' ($replay.Status -eq 401) "$($replay.Status)"

    Write-Host ""
    Write-Host "[4] Refresh com rotacao"
    $r1 = Http 'POST' '/api/auth/refresh' "{`"refreshToken`":`"$($script:refresh)`"}"
    $d1 = Json $r1.Corpo
    Verificar 'refresh valido retorna 200' ($r1.Status -eq 200) "$($r1.Status) $($r1.Corpo)"
    Verificar 'novo refresh e diferente do anterior' ($d1.refreshToken -ne $script:refresh)

    $r2 = Http 'POST' '/api/auth/refresh' "{`"refreshToken`":`"$($script:refresh)`"}"
    Verificar 'refresh antigo nao pode ser reusado' ($r2.Status -eq 401) "$($r2.Status)"

    Write-Host ""
    Write-Host "[5] Recuperacao de senha"
    $esq = Http 'POST' '/api/auth/esqueci-senha' "{`"email`":`"$Email`"}"
    Verificar 'esqueci-senha retorna 202' ($esq.Status -eq 202) "$($esq.Status) $($esq.Corpo)"
    Verificar 'resposta NAO contem codigo de 6 digitos' (-not ($esq.Corpo -match '\d{6}')) "$($esq.Corpo)"

    $esqFantasma = Http 'POST' '/api/auth/esqueci-senha' '{"email":"naoexiste@bsfm.dev"}'
    Verificar 'conta inexistente devolve a mesma resposta' (($esqFantasma.Status -eq 202) -and ($esqFantasma.Corpo -eq $esq.Corpo)) "$($esqFantasma.Corpo)"

    $resetRuim = Http 'POST' '/api/auth/redefinir-senha' "{`"email`":`"$Email`",`"codigo`":`"000000`",`"novaSenha`":`"NovaSenha#2026`"}"
    Verificar 'codigo errado e recusado com 400' ($resetRuim.Status -eq 400) "$($resetRuim.Status) $($resetRuim.Corpo)"
}

Write-Host ""
Write-Host "================  $($script:passou) PASS / $($script:falhou) FAIL  ================"
if ($script:falhou -gt 0) { exit 1 } else { exit 0 }
