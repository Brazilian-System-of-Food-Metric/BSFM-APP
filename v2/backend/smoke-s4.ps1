# ============================================================================
# Teste de fumaca da camada nutricional (rotas publicas /api/nutricao).
# Windows PowerShell 5.1. Sem acentos.
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
        UseBasicParsing = $true; TimeoutSec = 30
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

Write-Host ""
Write-Host "[1] Provedor de visao"
$prov = Http 'GET' '/api/nutricao/provedor'
$p = Json $prov.Corpo
Verificar 'GET /provedor responde 200' ($prov.Status -eq 200) "$($prov.Status)"
Verificar 'sem chave, provedor se declara indisponivel' ($p.disponivel -eq $false) "$($p.disponivel)"
Verificar 'mensagem explica o que fazer' ($p.mensagem -match 'BSFM_GEMINI_API_KEY') "$($p.mensagem)"

Write-Host ""
Write-Host "[2] Busca no catalogo"
$arroz = Json (Http 'GET' '/api/nutricao/alimentos?q=arroz').Corpo
Verificar 'busca por arroz retorna itens' ($arroz.total -ge 1) "$($arroz.total)"
Verificar 'primeiro item e um arroz' ($arroz.itens[0].nome -match 'Arroz') "$($arroz.itens[0].nome)"
Verificar 'traz kcal por 100 g plausivel' ($arroz.itens[0].por100g.kcal -gt 80 -and $arroz.itens[0].por100g.kcal -lt 200) "$($arroz.itens[0].por100g.kcal)"

$feijao = Json (Http 'GET' '/api/nutricao/alimentos?q=feij%C3%A3o').Corpo
Verificar 'busca com acento acha feijao carioca' ($feijao.itens[0].nome -match 'carioca') "$($feijao.itens[0].nome)"

$nada = Json (Http 'GET' '/api/nutricao/alimentos?q=xilofone').Corpo
Verificar 'consulta sem match retorna zero' ($nada.total -eq 0) "$($nada.total)"

Write-Host ""
Write-Host "[3] Analise por nome"
$porNome = Http 'POST' '/api/nutricao/por-nome' '{"nome":"arroz","gramas":200}'
$r = Json $porNome.Corpo
Verificar 'por-nome responde 200' ($porNome.Status -eq 200) "$($porNome.Status) $($porNome.Corpo)"
Verificar 'identifica um arroz' ($r.alimento.nome -match 'Arroz') "$($r.alimento.nome)"
Verificar 'dobra as calorias em 200 g' ($r.porcao.kcal -gt 150 -and $r.porcao.kcal -lt 350) "$($r.porcao.kcal)"
Verificar 'traz candidatos' (@($r.candidatos).Count -ge 1) "@($($r.candidatos).Count)"

$semMatch = Http 'POST' '/api/nutricao/por-nome' '{"nome":"xilofone quantico","gramas":100}'
$s = Json $semMatch.Corpo
Verificar 'nome fora do catalogo responde 422' ($semMatch.Status -eq 422) "$($semMatch.Status)"
Verificar 'motivo explicito' ($s.motivo -eq 'alimento_fora_do_catalogo') "$($s.motivo)"
Verificar 'nao devolve nutriente inventado' ($null -eq $s.porcao) "$($s.porcao)"

$semNome = Http 'POST' '/api/nutricao/por-nome' '{"nome":"","gramas":100}'
Verificar 'nome vazio responde 422' ($semNome.Status -eq 422) "$($semNome.Status)"

$porcaoZero = Json (Http 'POST' '/api/nutricao/por-nome' '{"nome":"banana","gramas":0}').Corpo
Verificar 'gramas 0 e limitado ao minimo' ($porcaoZero.gramas -eq 1) "$($porcaoZero.gramas)"

Write-Host ""
Write-Host "[4] Analise por imagem sem provedor"
$semChave = Http 'POST' '/api/nutricao/por-imagem' '{"imagemBase64":"aGVsbG8="}'
$sc = Json $semChave.Corpo
Verificar 'sem provedor responde 503' ($semChave.Status -eq 503) "$($semChave.Status) $($semChave.Corpo)"
Verificar 'motivo do 503 e a ausencia de configuracao' ($sc.motivo -eq 'provedor_de_visao_indisponivel') "$($sc.motivo)"
Verificar 'nao devolve resultado falso' ($sc.identificado -eq $false) "$($sc.identificado)"

Write-Host ""
Write-Host "[5] Validacao de entrada"
$semImagem = Http 'POST' '/api/nutricao/por-imagem' '{"dica":"arroz"}'
Verificar 'imagem ausente responde 400' ($semImagem.Status -eq 400) "$($semImagem.Status)"

$base64Ruim = Http 'POST' '/api/nutricao/por-imagem' '{"imagemBase64":"@@@nao-e-base64@@@"}'
Verificar 'base64 invalido responde 400' ($base64Ruim.Status -eq 400) "$($base64Ruim.Status)"

$vazia = Http 'POST' '/api/nutricao/por-imagem' '{"imagemBase64":""}'
Verificar 'imagem vazia responde 400' ($vazia.Status -eq 400) "$($vazia.Status)"

Write-Host ""
Write-Host "[6] Catalogo cobre o basico do prato brasileiro"
foreach ($termo in @('arroz', 'feijao', 'frango', 'ovo', 'banana', 'leite', 'pao', 'batata')) {
    $busca = Json (Http 'GET' "/api/nutricao/alimentos?q=$termo").Corpo
    Verificar "'$termo' tem correspondencia" ($busca.total -ge 1) "$($busca.total)"
}

Write-Host ""
Write-Host "================  $($script:passou) PASS / $($script:falhou) FAIL  ================"
if ($script:falhou -gt 0) { exit 1 } else { exit 0 }
