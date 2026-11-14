# ============================================================================
# Teste de fumaca da API S2 (controllers + EF + sessao assinada).
# Compatível com Windows PowerShell 5.1. Texto sem acentos de proposito.
# ============================================================================
param(
    [string]$BaseUrl = 'http://127.0.0.1:5100',
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
        Uri = "$BaseUrl$caminho"; Method = $metodo; Headers = $headers
        ContentType = 'application/json; charset=utf-8'
        UseBasicParsing = $true; TimeoutSec = 20
    }
    if ($corpo) { $params['Body'] = [System.Text.Encoding]::UTF8.GetBytes($corpo) }
    try {
        $r = Invoke-WebRequest @params
        return @{ Status = [int]$r.StatusCode; Corpo = [string]$r.Content }
    }
    catch {
        $resp = $_.Exception.Response
        if ($resp -ne $null) {
            $leitor = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
            $texto = $leitor.ReadToEnd(); $leitor.Dispose()
            return @{ Status = [int]$resp.StatusCode; Corpo = $texto }
        }
        return @{ Status = 0; Corpo = $_.Exception.Message }
    }
}

function ChaveBytes([string]$chave) {
    # base64url -> bytes
    $s = $chave.Replace('-', '+').Replace('_', '/')
    switch ($s.Length % 4) { 2 { $s += '==' } 3 { $s += '=' } 0 { } default { } }
    return [Convert]::FromBase64String($s)
}

function Assinar([string]$metodo, [string]$caminho, [string]$corpo) {
    $somentePath = ($caminho -split '\?')[0]
    $ts = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $nonce = [guid]::NewGuid().ToString('N')
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $corpoHash = Hex ($sha.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($corpo)))
    $sha.Dispose()
    $canonica = "$($metodo.ToUpperInvariant())`n$somentePath`n$ts`n$nonce`n$corpoHash"
    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    # Chave da sessao quando disponivel (o servidor deriva do jti); senao, a global.
    if ($script:chave) { $hmac.Key = ChaveBytes $script:chave }
    else { $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($HmacSecret) }
    $assinatura = Hex ($hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($canonica)))
    $hmac.Dispose()
    return @{ Timestamp = "$ts"; Nonce = $nonce; Signature = $assinatura }
}
function Autenticado([string]$metodo, [string]$caminho, [string]$corpo = '') {
    $s = Assinar $metodo $caminho $corpo
    return Http $metodo $caminho $corpo @{
        Authorization = "Bearer $($script:token)"
        'X-BSFM-Timestamp' = $s.Timestamp
        'X-BSFM-Nonce' = $s.Nonce
        'X-BSFM-Signature' = $s.Signature
    }
}

function Json([string]$texto) {
    if ([string]::IsNullOrWhiteSpace($texto)) { return $null }
    try { return $texto | ConvertFrom-Json } catch { return $null }
}

Write-Host ""
Write-Host "[0] Servico no ar"
$saude = Http 'GET' '/saude'
Verificar 'GET /saude responde 200' ($saude.Status -eq 200) "$($saude.Status)"

Write-Host ""
Write-Host "[1] Login e emissao de sessao"
$login = Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"$Senha`"}"
$dados = Json $login.Corpo
Verificar 'login retorna 200' ($login.Status -eq 200) "$($login.Status) $($login.Corpo)"
Verificar 'token emitido' (-not [string]::IsNullOrEmpty($dados.token))
Verificar 'refresh emitido' (-not [string]::IsNullOrEmpty($dados.refreshToken))
    Verificar 'chave de assinatura por sessao emitida' (-not [string]::IsNullOrEmpty($dados.chaveAssinatura))
Verificar 'resposta traz IMC calculado' ($dados.usuario.imc -gt 0) "$($dados.usuario.imc)"
Verificar 'resposta traz classificacao do IMC' (-not [string]::IsNullOrEmpty($dados.usuario.classificacaoImc))
$script:token = $dados.token
$script:refresh = $dados.refreshToken
    $script:chave = $dados.chaveAssinatura

$loginBia = Http 'POST' '/api/auth/login' '{"email":"bia@bsfm.dev","senha":"SenhaForte#2026"}'
Verificar 'conta com 2FA exige codigo' ($loginBia.Status -eq 401) "$($loginBia.Status)"

# Hash legado BCrypt: precisa autenticar e ser promovido para PBKDF2 no login.
$loginCarla = Http 'POST' '/api/auth/login' '{"email":"carla@bsfm.dev","senha":"SenhaForte#2026"}'
Verificar 'hash legado BCrypt autentica (ponte de compatibilidade)' ($loginCarla.Status -eq 200) "$($loginCarla.Status) $($loginCarla.Corpo)"

Write-Host ""
Write-Host "[2] Rota antiga por id nao existe mais"
$idor = Http 'GET' '/api/saude/1'
Verificar 'GET /api/saude/1 nao resolve (404)' ($idor.Status -eq 404) "$($idor.Status)"

Write-Host ""
Write-Host "[3] Perfil: identidade vem do token"
$semAssinatura = Http 'GET' '/api/eu/perfil' '' @{ Authorization = "Bearer $($script:token)" }
Verificar 'sem assinatura retorna 401' ($semAssinatura.Status -eq 401) "$($semAssinatura.Status)"

$perfil = Autenticado 'GET' '/api/eu/perfil'
$p = Json $perfil.Corpo
Verificar 'perfil assinado retorna 200' ($perfil.Status -eq 200) "$($perfil.Status) $($perfil.Corpo)"
Verificar 'id vem da sessao' ($p.id -eq 1) "$($p.id)"
Verificar 'jti exposto para auditoria' (-not [string]::IsNullOrEmpty($p.jti))

Write-Host ""
Write-Host "[4] Atualizacao de nome"
$renomear = Autenticado 'PUT' '/api/eu/nome' '{"nome":"Ana Souza Silva"}'
Verificar 'PUT nome retorna 200' ($renomear.Status -eq 200) "$($renomear.Status) $($renomear.Corpo)"

$curto = Autenticado 'PUT' '/api/eu/nome' '{"nome":"Ab"}'
Verificar 'nome curto e recusado' ($curto.Status -eq 400) "$($curto.Status)"

$perfil2 = Json (Autenticado 'GET' '/api/eu/perfil').Corpo
Verificar 'nome novo persiste' ($perfil2.nome -eq 'Ana Souza Silva') "$($perfil2.nome)"

Write-Host ""
Write-Host "[5] Dados de saude"
$saudeGet = Autenticado 'GET' '/api/eu/saude'
$s = Json $saudeGet.Corpo
Verificar 'GET saude retorna 200' ($saudeGet.Status -eq 200) "$($saudeGet.Status)"
Verificar 'agua alvo calculada' ($s.aguaAlvoMl -ge 1500) "$($s.aguaAlvoMl)"

$saudePut = Autenticado 'PUT' '/api/eu/saude' '{"peso":66.5,"altura":1.68,"aceitouTermos":true,"versaoTermos":"1.1"}'
$s2 = Json $saudePut.Corpo
Verificar 'PUT saude retorna 200' ($saudePut.Status -eq 200) "$($saudePut.Status) $($saudePut.Corpo)"
Verificar 'IMC recalculado no servidor' ($s2.imc -gt 23 -and $s2.imc -lt 24) "$($s2.imc)"
Verificar 'TMB recalculada' ($s2.tmb -gt 1000) "$($s2.tmb)"

$pesoInvalido = Autenticado 'PUT' '/api/eu/saude' '{"peso":900}'
Verificar 'peso absurdo e recusado' ($pesoInvalido.Status -eq 400) "$($pesoInvalido.Status)"

$hist = Autenticado 'GET' '/api/eu/saude/historico'
$h = Json $hist.Corpo
Verificar 'historico registra a medicao' ($h.Count -ge 1) "$($h.Count)"

Write-Host ""
Write-Host "[6] Sessoes"
$sessoes = Autenticado 'GET' '/api/eu/sessoes'
$lista = Json $sessoes.Corpo
Verificar 'lista de sessoes retorna 200' ($sessoes.Status -eq 200) "$($sessoes.Status)"
# PS 5.1 devolve objeto unico (sem .Count) quando o array tem um elemento: forcar array.
$marcadas = @($lista | Where-Object { $_.atual -eq $true })
Verificar 'sessao atual marcada' ($marcadas.Count -eq 1) "$($sessoes.Corpo)"

Write-Host ""
Write-Host "[7] Troca de senha"
$senhaErrada = Autenticado 'PUT' '/api/eu/senha' '{"senhaAtual":"errada","novaSenha":"NovaSenha#2026"}'
Verificar 'senha atual errada e recusada' ($senhaErrada.Status -eq 400) "$($senhaErrada.Status)"

$senhaOk = Autenticado 'PUT' '/api/eu/senha' "{`"senhaAtual`":`"$Senha`",`"novaSenha`":`"NovaSenha#2026`"}"
Verificar 'troca de senha retorna 200' ($senhaOk.Status -eq 200) "$($senhaOk.Status) $($senhaOk.Corpo)"

$loginAntigo = Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"$Senha`"}"
Verificar 'senha antiga nao funciona mais' ($loginAntigo.Status -eq 401) "$($loginAntigo.Status)"

Write-Host ""
Write-Host "[8] Logout e refresh"
$logout = Http 'POST' '/api/auth/logout' "{`"refreshToken`":`"$($script:refresh)`"}"
Verificar 'logout retorna 204' ($logout.Status -eq 204) "$($logout.Status)"

$refreshDepois = Http 'POST' '/api/auth/refresh' "{`"refreshToken`":`"$($script:refresh)`"}"
Verificar 'refresh apos logout e recusado' ($refreshDepois.Status -eq 401) "$($refreshDepois.Status)"

Write-Host ""
Write-Host "[9] Bloqueio progressivo por forca bruta"
for ($i = 1; $i -le 5; $i++) {
    Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"errada$i`"}" | Out-Null
}
$posBloqueio = Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"NovaSenha#2026`"}"
Verificar 'conta bloqueia apos 5 falhas mesmo com senha certa' ($posBloqueio.Status -eq 401) "$($posBloqueio.Status)"

Write-Host ""
Write-Host "================  $($script:passou) PASS / $($script:falhou) FAIL  ================"
if ($script:falhou -gt 0) { exit 1 } else { exit 0 }
