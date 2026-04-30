using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks; // ADICIONADO: Necessário para o Task.Run
using MimeKit;
using MailKit.Security;
using SmtpClient = MailKit.Net.Smtp.SmtpClient; 
using System.Net.Http;
using System.Net.Http.Json; // Importante para o JsonContent
using System.Net.Http.Headers;

namespace ClassesBSFM
{
    public class Usuario
    {
        [Key]
        public int ID { get; set; }

        public string Nome { get; set; } = string.Empty;
        public int Idade { get; set; }
        public string Email { get; set; } = string.Empty;

        public string? TokenVerificacao { get; set; }
        public bool EmailVerificado { get; set; } = false;

        public string SenhaHash { get; set; } = string.Empty; 
        public bool AceitouTermos { get; set; }
        public DateTime DataAceite { get; set; }
        public string VersaoTermos { get; set; } = string.Empty;
        public string Sexo { get; set; } = "Não Informado"; 
        public double Peso { get; set; }
        public double Altura { get; set; }
        public string TipoPessoa { get; set; } = "Sedentário";
        public string Intolerancia { get; set; } = string.Empty; 
        
        public double IMC { get; set; }
        public double TMB { get; set; }
        public double GastoTotal { get; set; }
        public double PesoMeta { get; set; } // O peso que o usuário quer atingir

        // NOVO: Data de Nascimento para cálculo automático da idade
        public DateTime? DataNascimento { get; set; }

        public Usuario() {
            AceitouTermos = false;
            DataAceite = DateTime.UtcNow; 
        }

        // Propriedade calculada: idade baseada na DataNascimento
        public int CalcularIdade()
        {
            if (DataNascimento == null) return Idade; // fallback para o valor antigo
            var hoje = DateTime.Today;
            var idade = hoje.Year - DataNascimento.Value.Year;
            if (DataNascimento.Value.Date > hoje.AddYears(-idade)) idade--;
            return idade;
        }
    }

    // NOVA CLASSE: Consumo de Água
    public class ConsumoAgua
    {
        [Key]
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public double Ml { get; set; } // Quantidade em ml
        public DateTime DataRegistro { get; set; } = DateTime.Now;
    }

    // NOVA CLASSE: RefeicaoAgendada (pratos da semana)
    public class RefeicaoAgendada
    {
        [Key]
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string DiaSemana { get; set; } = string.Empty; // "Segunda", "Terca", etc.
        public string TipoRefeicao { get; set; } = string.Empty; // "Café", "Almoço", "Jantar", "Lanche"
        public string NomePrato { get; set; } = string.Empty;
        public string Ingredientes { get; set; } = string.Empty;
        public string ModoPreparo { get; set; } = string.Empty;
        public double Calorias { get; set; }
        public double Proteinas { get; set; }
        public double Carboidratos { get; set; }
        public double Gorduras { get; set; }
        public DateTime DataCriacao { get; set; } = DateTime.Now;
    }

    public class CalcularNutricional
    {
        public double CalcularIMC(double peso, double altura)
        {
            if (altura <= 0) return 0;
            return peso / (altura * altura);
        }

        public double CalcularTMB(string sexo, double peso, double alturaMetros, int idade)
        {
            double alturaCm = alturaMetros * 100;
            if (sexo?.ToLower() == "masculino")
                return 88.362 + (13.397 * peso) + (4.799 * alturaCm) - (5.677 * idade);
            else
                return 447.593 + (9.247 * peso) + (3.098 * alturaCm) - (4.330 * idade);
        }

        public double CalcularGastoTotal(string nivelAtividade, double tmb)
        {
            string nivel = nivelAtividade?.ToLower() ?? "";
            if (nivel.Contains("sedentario") || nivel.Contains("sedentário")) return tmb * 1.2;
            if (nivel == "ativo") return tmb * 1.55;
            if (nivel == "muito ativo") return tmb * 1.725;
            return tmb;
        }

        public void RegistrarCalculos(Usuario usuario)
        {
            usuario.IMC = Math.Round(CalcularIMC(usuario.Peso, usuario.Altura), 2);
            usuario.TMB = Math.Round(CalcularTMB(usuario.Sexo, usuario.Peso, usuario.Altura, usuario.Idade), 2);
            usuario.GastoTotal = Math.Round(CalcularGastoTotal(usuario.TipoPessoa, usuario.TMB), 2);
        }
    }

    public class Refeição
    {
        [Key]
        public int ID { get; set; }
        public string NomeRefeição { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty; 
        public string Ingredientes { get; set; } = string.Empty;
        public double Calorias { get; set; }
        public double Proteínas { get; set; }
        public double Carboidratos { get; set; }
        public double Gorduras { get; set; }
    }

    public class Comida
    {
        [Key]
        public int ID { get; set; }
        public string NomeComida { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public double Calorias { get; set; }
        public double Proteínas { get; set; }
        public double Carboidratos { get; set; }
        public double Gorduras { get; set; }
    }

    public class CronogramaAlimentar
    {
        [Key]
        public int ID { get; set; }
        public Usuario? Usuario { get; set; }
        public string Refeições { get; set; } = string.Empty;
        public string Planos { get; set; } = string.Empty;

        public CronogramaAlimentar()
        {
            Refeições = string.Empty;
        }
    }

    public class Hospital
    {
        [Key]
        public int ID { get; set; }
        public string NomeHospital { get; set; } = string.Empty;
        public string Endereço { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
    }

 public class EmailService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        // RECOMENDAÇÃO: Pegue a chave do Railway por segurança. 
        // Se preferir fixo, substitua o GetEnvironmentVariable pela sua string da API Key entre aspas.
        private static string apiKey => Environment.GetEnvironmentVariable("BREVO_API_KEY") ?? "chave_nao_encontrada";

        // IMPORTANTE: Este e-mail deve ser o GMAIL que você validou no Brevo
        private const string emailRemetente = "brazilian.sfm@gmail.com"; 

        public static void EnviarToken(string emailDestino, string token)
        {
            // Executa em segundo plano para não travar a resposta do site/app
            _ = Task.Run(async () =>
            {
                try
                {
                    const string url = "https://api.brevo.com/v3/smtp/email";

                    var payload = new
                    {
                        sender = new { name = "Portal BSFM", email = emailRemetente },
                        to = new[] { new { email = emailDestino, name = "Usuario BSFM" } },
                        subject = "🔐 Código de Segurança: " + token,
                        htmlContent = $@"
                        <div style='font-family: sans-serif; background-color: #ffffff; padding: 40px; border: 1px solid #e5e7eb; border-radius: 24px; max-width: 500px; margin: auto;'>
                            <div style='text-align: center; margin-bottom: 30px;'>
                                <div style='background-color: #059669; display: inline-block; padding: 15px; border-radius: 18px;'>
                                    <span style='color: white; font-size: 24px;'>BSFM</span>
                                </div>
                            </div>
                            <h2 style='color: #111827; text-align: center; font-size: 20px;'>Verificação de Acesso</h2>
                            <p style='color: #4b5563; text-align: center; font-size: 16px; line-height: 1.5;'>Olá! Utilize o código abaixo para validar sua identidade e proteger sua conta no Portal Nutricional.</p>
                            
                            <div style='background-color: #f0fdf4; border: 2px dashed #10b981; margin: 30px 0; padding: 25px; text-align: center; border-radius: 20px;'>
                                <span style='font-size: 42px; font-weight: bold; color: #047857; letter-spacing: 12px; display: block;'>{token}</span>
                            </div>
                            
                            <p style='color: #9ca3af; text-align: center; font-size: 12px; margin-top: 30px;'>
                                Este é um e-mail automático do Sistema BSFM. <br> 
                                Se você não solicitou este código, por favor ignore.
                            </p>
                        </div>"
                    };

                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    
                    // Cabeçalho obrigatório do Brevo
                    request.Headers.Add("api-key", apiKey);
                    request.Content = JsonContent.Create(payload);

                    var response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[BREVO SUCCESS] Token {token} enviado com sucesso para {emailDestino}.");
                    }
                    else
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[BREVO ERROR] Status: {response.StatusCode} - {error}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BREVO FATAL EXCEPTION] Falha crítica no envio: {ex.Message}");
                }
            });
        }
    }
    public class AnaliseIA
{
    [Key]
    public int ID { get; set; }
    
    public int UsuarioID { get; set; } // Vínculo com o usuário
    public string Alimento { get; set; } = string.Empty;
    public double Calorias { get; set; }
    public double Proteinas { get; set; }
    public double Carbos { get; set; }
    public double Gorduras { get; set; }
    public string Porcao { get; set; } = string.Empty;
    public DateTime DataAnalise { get; set; } = DateTime.Now; // Usado para exclusão
}

public class HistoricoProgresso
{
    [Key]
    public int ID { get; set; }
    public int UsuarioID { get; set; }
    public double Peso { get; set; }
    public double Altura { get; set; }
    public double IMC { get; set; }
    public DateTime DataRegistro { get; set; } = DateTime.Now;
}

}


