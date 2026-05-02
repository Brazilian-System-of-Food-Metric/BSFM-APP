using System;
using System.Collections.Generic;
using System.Linq; // ADICIONADO: Essencial para o FirstOrDefault funcionar
using ClassesBSFM;
using PonteBanco;
using BCrypt.Net; // Garante que o BCrypt seja reconhecido

namespace BSFM
{
    public class UsuarioService
    {
        private CalcularNutricional _calculadora = new CalcularNutricional();

        // Método para montar o objeto de usuário com segurança
        public Usuario CriarNovoUsuario(string nome, int idade, string email, double peso, double altura, string sexo, string tipoPessoa, bool aceitou, string intolerancias, string senhaPura)
        {
            Usuario novoUser = new Usuario
            {
                Nome = nome,
                Idade = idade,
                Email = email.Trim().ToLower(), // Salva sempre limpo e em minúsculo
                SenhaHash = BCrypt.Net.BCrypt.HashPassword(senhaPura),
                Peso = peso,
                Altura = altura,
                Sexo = sexo,
                TipoPessoa = tipoPessoa,
                AceitouTermos = aceitou,
                DataAceite = DateTime.Now,
                VersaoTermos = "v1.0",
                Intolerancia = intolerancias
            };

            // Processa os cálculos automáticos
            _calculadora.RegistrarCalculos(novoUser);

            return novoUser;
        }

        // Método para efetivar o salvamento no SQLite
        public void SalvarNoBancoDeDados(Usuario usuario)
        {
            using (var db = new PonteDB())
            {
                db.Database.EnsureCreated(); // Garante que as tabelas existam
                db.Usuarios.Add(usuario);
                db.SaveChanges();
                Console.WriteLine($"[SISTEMA] Usuário {usuario.Nome} persistido no banco com sucesso.");
            }
        }

        // Lógica de Autenticação (Login)
        public (bool sucesso, Usuario? usuario) Autenticar(string email, string senhaDigitada)
        {
            using (var db = new PonteDB())
            {
                string emailTratado = email.Trim().ToLower();
                
                Console.WriteLine($"[LOGIN] Tentativa para: {emailTratado}");

                // Procura o usuário no banco (com System.Linq)
                var usuario = db.Usuarios.FirstOrDefault(u => u.Email.ToLower() == emailTratado);

                if (usuario == null) 
                {
                    Console.WriteLine("[LOGIN] Falha: E-mail não encontrado.");
                    return (false, null);
                }

                // Verifica a criptografia
                bool senhaCorreta = BCrypt.Net.BCrypt.Verify(senhaDigitada.Trim(), usuario.SenhaHash);

                if (senhaCorreta) 
                {
                    Console.WriteLine($"[LOGIN] Sucesso: {usuario.Nome} entrou.");
                    return (true, usuario);
                }

                Console.WriteLine("[LOGIN] Falha: Senha incorreta.");
                return (false, null);
            }
        }
    }
}

