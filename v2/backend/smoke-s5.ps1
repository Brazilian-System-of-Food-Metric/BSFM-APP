# ============================================================================
# Teste de fumaca do cadastro (S5). Windows PowerShell 5.1. Sem acentos.
# Cobre a rota que faltava: sem ela, criar conta retornava 404.
# ============================================================================
param(
    [string]$BaseUrl = 'http://127.0.0.1:5100'
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

$script:passou = 0
$script:falhou = 0

function Verificar([string]$nome, $condicao, [string]$detalhe = '') {
    if ([bool]$condicao) { $script:passou++; Write-Host "  PASS  $nome" }
    else { $script:falhou++; Write-Host "  FAIL  $nome  -> $detalhe" -ForegroundColor Red }
}

function Http([string]$metodo, [string]$caminho, [string]$corpo = '') {
    $params = @{
        Uri = "$BaseUrl$caminho"; Method = $metodo
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

function Json([string]$texto) {
    if ([string]::IsNullOrWhiteSpace($texto)) { return $null }
    try { return $texto | ConvertFrom-Json } catch { return $null }
}

# E-mail unico por execucao: o suite pode rodar varias vezes
$email = "teste.$([guid]::NewGuid().ToString('N').Substring(0,10))@bsfm.dev"
$senha = 'SenhaForte#2026'

Write-Host ""
Write-Host "[1] Cadastro"
$corpo = @{
    nome = 'Pessoa de Teste'
    email = $email
    senha = $senha
    peso = 72.5
    altura = 1.75
    sexo = 'Masculino'
    nivelAtividade = 'Ativo'
    aceitouTermos = $true
    versaoTermos = '1.0'
} | ConvertTo-Json -Compress

$criar = Http 'POST' '/api/auth/cadastrar' $corpo
$criado = Json $criar.Corpo
Verificar 'cadastro retorna 201' ($criar.Status -eq 201) "$($criar.Status) $($criar.Corpo)"
Verificar 'resposta traz id' ($criado.id -gt 0) "$($criado.id)"
Verificar 'e-mail nasce nao verificado' ($criado.emailVerificado -eq $false) "$($criado.emailVerificado)"
Verificar 'resposta NAO devolve codigo' (-not ($criar.Corpo -match '\d{6}')) "$($criar.Corpo)"

Write-Host ""
Write-Host "[2] Duplicidade e validacao"
$dup = Http 'POST' '/api/auth/cadastrar' $corpo
Verificar 'e-mail duplicado retorna 409' ($dup.Status -eq 409) "$($dup.Status) $($dup.Corpo)"

$dupMaiusculo = $corpo.Replace($email, $email.ToUpperInvariant())
$dup2 = Http 'POST' '/api/auth/cadastrar' $dupMaiusculo
Verificar 'duplicidade ignora maiusculas' ($dup2.Status -eq 409) "$($dup2.Status)"

$curta = @{ nome = 'Ab'; email = "x$email"; senha = '123'; aceitouTermos = $true } | ConvertTo-Json -Compress
$r = Http 'POST' '/api/auth/cadastrar' $curta
Verificar 'nome/senha curtos sao recusados' ($r.Status -eq 400) "$($r.Status)"

$semTermos = @{ nome = 'Pessoa Ok'; email = "y$email"; senha = $senha; aceitouTermos = $false } | ConvertTo-Json -Compress
$r2 = Http 'POST' '/api/auth/cadastrar' $semTermos
Verificar 'sem aceitar termos e recusado' ($r2.Status -eq 400) "$($r2.Status)"

$emailRuim = @{ nome = 'Pessoa Ok'; email = 'nao-e-email'; senha = $senha; aceitouTermos = $true } | ConvertTo-Json -Compress
$r3 = Http 'POST' '/api/auth/cadastrar' $emailRuim
Verificar 'e-mail invalido e recusado' ($r3.Status -eq 400) "$($r3.Status)"

Write-Host ""
Write-Host "[3] Login com a conta nova"
$login = Http 'POST' '/api/auth/login' "{`"email`":`"$email`",`"senha`":`"$senha`"}"
$dados = Json $login.Corpo
Verificar 'login funciona apos cadastro' ($login.Status -eq 200) "$($login.Status) $($login.Corpo)"
Verificar 'IMC calculado no servidor' ($dados.usuario.imc -gt 23 -and $dados.usuario.imc -lt 24) "$($dados.usuario.imc)"
Verificar 'token e chave emitidos' ((-not [string]::IsNullOrEmpty($dados.token)) -and (-not [string]::IsNullOrEmpty($dados.chaveAssinatura)))

Write-Host ""
Write-Host "[4] Verificacao de e-mail"
$errado = Http 'POST' '/api/auth/verificar-email' "{`"email`":`"$email`",`"codigo`":`"000000`"}"
Verificar 'codigo errado retorna 400' ($errado.Status -eq 400) "$($errado.Status) $($errado.Corpo)"

$inexistente = Http 'POST' '/api/auth/verificar-email' '{"email":"naoexiste@bsfm.dev","codigo":"123456"}'
Verificar 'e-mail inexistente retorna 400' ($inexistente.Status -eq 400) "$($inexistente.Status)"

Write-Host ""
Write-Host "================  $($script:passou) PASS / $($script:falhou) FAIL  ================"
if ($script:falhou -gt 0) { exit 1 } else { exit 0 }
