# ============================================================================
# Teste de fumaca dos grupos migrados no S3: agua, refeicoes, metas, analises,
# evolucao e tratamento de erro.
#
# Idempotente: as assercoes usam DELTA em relacao ao estado inicial, entao o
# suite passa em banco limpo ou ja populado. Windows PowerShell 5.1. Sem acentos.
# ============================================================================
param(
    [string]$BaseUrl = 'http://127.0.0.1:5100',
    # Usuaria distinta do S2: o smoke do S2 bloqueia a ana por forca bruta no final.
    [string]$Email = 'carla@bsfm.dev',
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

function Lista([string]$texto) {
    if ([string]::IsNullOrWhiteSpace($texto)) { return @() }
    try { return @($texto | ConvertFrom-Json) } catch { return @() }
}

function Perto([double]$a, [double]$b) { return [math]::Abs($a - $b) -lt 0.01 }

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "[0] Sessao"
$login = Http 'POST' '/api/auth/login' "{`"email`":`"$Email`",`"senha`":`"$Senha`"}"
$dados = Json $login.Corpo
Verificar 'login ok' ($login.Status -eq 200) "$($login.Status) $($login.Corpo)"
$script:token = $dados.token
$script:chave = $dados.chaveAssinatura

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "[1] Agua"
$aguaInicial = (Json (Autenticado 'GET' '/api/eu/agua').Corpo).totalMl

$a1 = Json (Autenticado 'POST' '/api/eu/agua' '{"ml":250}').Corpo
Verificar 'registra 250 ml' (Perto $a1.totalMl ($aguaInicial + 250)) "$($a1.totalMl) base=$aguaInicial"

$a2 = Json (Autenticado 'POST' '/api/eu/agua' '{"ml":300}').Corpo
Verificar 'acumula mais 300 ml' (Perto $a2.totalMl ($aguaInicial + 550)) "$($a2.totalMl)"
Verificar 'percentual da meta calculado' ($a2.percentual -gt 0) "$($a2.percentual)"

$zero = Autenticado 'POST' '/api/eu/agua' '{"ml":0}'
Verificar 'quantidade zero e recusada' ($zero.Status -eq 400) "$($zero.Status)"

$exagero = Autenticado 'POST' '/api/eu/agua' '{"ml":5000}'
Verificar 'quantidade acima do limite e recusada' ($exagero.Status -eq 400) "$($exagero.Status)"

$semana = Json (Autenticado 'GET' '/api/eu/agua/semanal').Corpo
$serieSemana = @($semana.serie)
Verificar 'semanal devolve 7 dias' ($serieSemana.Count -eq 7) "count=$($serieSemana.Count)"

$remove = Autenticado 'DELETE' '/api/eu/agua/ultimo'
Verificar 'remove ultimo registro' ($remove.Status -eq 204) "$($remove.Status)"

$depois = Json (Autenticado 'GET' '/api/eu/agua').Corpo
Verificar 'total volta ao valor inicial + 250' (Perto $depois.totalMl ($aguaInicial + 250)) "$($depois.totalMl)"

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "[2] Refeicoes"
$refInicial = (Json (Autenticado 'GET' '/api/eu/refeicoes').Corpo).total

$cria = Autenticado 'POST' '/api/eu/refeicoes' '{"diaSemana":"Segunda","tipoRefeicao":"Almoco","nomePrato":"Frango com arroz","calorias":520,"proteinas":40,"carboidratos":55,"gorduras":12}'
$r1 = Json $cria.Corpo
Verificar 'cria refeicao (201)' ($cria.Status -eq 201) "$($cria.Status) $($cria.Corpo)"

$comAcento = Autenticado 'POST' '/api/eu/refeicoes' '{"diaSemana":"Terca","tipoRefeicao":"Jantar","nomePrato":"Sopa","calorias":300}'
Verificar 'aceita dia valido' ($comAcento.Status -eq 201) "$($comAcento.Status)"

$diaRuim = Autenticado 'POST' '/api/eu/refeicoes' '{"diaSemana":"Ferias","tipoRefeicao":"Almoco","nomePrato":"X","calorias":10}'
Verificar 'dia invalido e recusado' ($diaRuim.Status -eq 400) "$($diaRuim.Status)"

$tipoRuim = Autenticado 'POST' '/api/eu/refeicoes' '{"diaSemana":"Segunda","tipoRefeicao":"Ceia","nomePrato":"X","calorias":10}'
Verificar 'tipo invalido e recusado' ($tipoRuim.Status -eq 400) "$($tipoRuim.Status)"

$lista = Json (Autenticado 'GET' '/api/eu/refeicoes').Corpo
Verificar 'duas refeicoes a mais' ($lista.total -eq $refInicial + 2) "$($lista.total) base=$refInicial"

$deOutro = Autenticado 'DELETE' '/api/eu/refeicoes/9999'
Verificar 'remover refeicao alheia/inexistente da 404' ($deOutro.Status -eq 404) "$($deOutro.Status)"

$propria = Autenticado 'DELETE' "/api/eu/refeicoes/$($r1.id)"
Verificar 'remove a propria refeicao' ($propria.Status -eq 204) "$($propria.Status)"

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "[3] Metas"
$metaInicial = Json (Autenticado 'GET' '/api/eu/metas').Corpo
Verificar 'meta derivada quando nao definida' ($metaInicial.caloriasAlvo -gt 0) "$($metaInicial.caloriasAlvo)"

$define = Autenticado 'PUT' '/api/eu/metas' '{"caloriasAlvo":1800,"proteinasAlvo":110,"aguaAlvoMl":2400,"pesoAlvo":64}'
Verificar 'define meta (200)' ($define.Status -eq 200) "$($define.Status) $($define.Corpo)"

$meta = Json (Autenticado 'GET' '/api/eu/metas').Corpo
Verificar 'meta refletida' (($meta.caloriasAlvo -eq 1800) -and ($meta.aguaAlvoMl -eq 2400)) "$($meta.caloriasAlvo)/$($meta.aguaAlvoMl)"

$metaRuim = Autenticado 'PUT' '/api/eu/metas' '{"caloriasAlvo":-5}'
Verificar 'meta invalida e recusada' ($metaRuim.Status -eq 400) "$($metaRuim.Status)"

$histMetas = Lista (Autenticado 'GET' '/api/eu/metas/historico').Corpo
Verificar 'historico de metas registra a definicao' ($histMetas.Count -ge 1) "count=$($histMetas.Count)"

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "[4] Analises"
$anInicial = (Lista (Autenticado 'GET' '/api/eu/analises').Corpo).Count
$resumoInicial = (Json (Autenticado 'GET' '/api/eu/analises/resumo').Corpo).calorias

$boa = Autenticado 'POST' '/api/eu/analises' '{"alimento":"Maca","calorias":52,"proteinas":0.3,"carbos":14,"gorduras":0.2,"porcao":"100g","confianca":0.92,"modeloVersao":"bsfmv1"}'
$an1 = Json $boa.Corpo
Verificar 'analise confiavel e registrada (201)' ($boa.Status -eq 201) "$($boa.Status) $($boa.Corpo)"

$ruim = Autenticado 'POST' '/api/eu/analises' '{"alimento":"Prato","calorias":400,"confianca":0.2,"origem":"foto"}'
Verificar 'confianca abaixo do minimo e rejeitada (422)' ($ruim.Status -eq 422) "$($ruim.Status)"

$manual = Autenticado 'POST' '/api/eu/analises' '{"alimento":"Prato","calorias":400,"confianca":0.2,"origem":"manual"}'
Verificar 'registro manual passa mesmo com confianca baixa' ($manual.Status -eq 201) "$($manual.Status)"

$macroRuim = Autenticado 'POST' '/api/eu/analises' '{"alimento":"Teste","calorias":100,"proteinas":900}'
Verificar 'macro absurdo e recusado' ($macroRuim.Status -eq 400) "$($macroRuim.Status)"

$listaAn = Lista (Autenticado 'GET' '/api/eu/analises').Corpo
Verificar 'duas analises a mais' ($listaAn.Count -eq $anInicial + 2) "count=$($listaAn.Count) base=$anInicial"

$resumo = Json (Autenticado 'GET' '/api/eu/analises/resumo').Corpo
Verificar 'resumo soma mais 452 calorias' (Perto $resumo.calorias ($resumoInicial + 452)) "$($resumo.calorias) base=$resumoInicial"

$delAnalise = Autenticado 'DELETE' "/api/eu/analises/$($an1.id)"
Verificar 'remove analise propria' ($delAnalise.Status -eq 204) "$($delAnalise.Status)"

# Servidor calcula os nutrientes: o cliente manda nome e gramas, nunca calorias.
$previa = Json (Http 'POST' '/api/nutricao/por-nome' '{"nome":"arroz","gramas":150}').Corpo
$doCatalogo = Autenticado 'POST' '/api/eu/analises/do-catalogo' '{"nome":"arroz","gramas":150}'
$dc = Json $doCatalogo.Corpo
Verificar 'registra do catalogo (201)' ($doCatalogo.Status -eq 201) "$($doCatalogo.Status) $($doCatalogo.Corpo)"
Verificar 'servidor calcula o mesmo que a previa publica' ($dc.calorias -eq $previa.porcao.kcal) "registrado=$($dc.calorias) previa=$($previa.porcao.kcal)"
Verificar 'porcao formatada pelo servidor' ($dc.porcao -eq '150g') "$($dc.porcao)"

$doCatalogoRuim = Autenticado 'POST' '/api/eu/analises/do-catalogo' '{"nome":"xilofone quantico","gramas":100}'
Verificar 'fora do catalogo recusa com 422' ($doCatalogoRuim.Status -eq 422) "$($doCatalogoRuim.Status)"

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "[5] Evolucao"
$evolucao = Autenticado 'GET' '/api/eu/evolucao'
$e = Json $evolucao.Corpo
Verificar 'evolucao retorna 200' ($evolucao.Status -eq 200) "$($evolucao.Status)"
Verificar 'serie tem ao menos um ponto' (@($e.serie).Count -ge 1) "@($($e.serie).Count)"
Verificar 'distancia da meta calculada' ($null -ne $e.distanciaDaMeta) "$($e.distanciaDaMeta)"

# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "[6] Erro padronizado"
$inexistente = Http 'GET' '/api/eu/inexistente' '' @{ Authorization = "Bearer $($script:token)" }
Verificar 'rota inexistente da 404' ($inexistente.Status -eq 404) "$($inexistente.Status)"

$semSessao = Http 'POST' '/api/eu/agua' '{"ml":100}'
Verificar 'escrita sem sessao da 401' ($semSessao.Status -eq 401) "$($semSessao.Status)"

$semAssinatura = Http 'POST' '/api/eu/agua' '{"ml":100}' @{ Authorization = "Bearer $($script:token)" }
Verificar 'escrita sem assinatura da 401' ($semAssinatura.Status -eq 401) "$($semAssinatura.Status)"

Write-Host ""
Write-Host "================  $($script:passou) PASS / $($script:falhou) FAIL  ================"
if ($script:falhou -gt 0) { exit 1 } else { exit 0 }
