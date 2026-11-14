using System.Text;

namespace BSFM.Nutricao;

/// <summary>Catálogo em uso pelo processo, com a origem declarada.</summary>
public interface ICatalogoNutricional
{
    IReadOnlyList<AlimentoCatalogo> Alimentos { get; }

    /// <summary>De onde veio: <c>taco</c> (CSV) ou <c>semente</c> (embutido).</summary>
    string Origem { get; }
}

/// <summary>
/// Carrega o catálogo da TACO a partir do CSV versionado e cai para a semente
/// embutida se o arquivo não existir ou estiver corrompido.
///
/// A semente existe para o projeto nunca subir sem catálogo: 54 itens comuns
/// valem mais que uma tela de erro.
/// </summary>
public sealed class CatalogoNutricional : ICatalogoNutricional
{
    private const int MinimoParaAceitar = 50;

    public CatalogoNutricional(string? caminhoArquivo = null)
    {
        if (!string.IsNullOrWhiteSpace(caminhoArquivo) && File.Exists(caminhoArquivo))
        {
            try
            {
                var linhas = File.ReadAllLines(caminhoArquivo, Encoding.UTF8);
                var carregado = CatalogoAlimentos.CarregarCsv(linhas);

                if (carregado.Count >= MinimoParaAceitar)
                {
                    Alimentos = carregado;
                    Origem = "taco";
                    return;
                }
            }
            catch (IOException)
            {
                // Arquivo bloqueado ou ilegível: segue com a semente.
            }
        }

        Alimentos = CatalogoAlimentos.Padrao;
        Origem = "semente";
    }

    public IReadOnlyList<AlimentoCatalogo> Alimentos { get; }

    public string Origem { get; }
}
