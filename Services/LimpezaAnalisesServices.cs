using Microsoft.Extensions.Hosting;
using PonteBanco;
using Microsoft.EntityFrameworkCore;

public class LimpezaAnalisesService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public LimpezaAnalisesService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
    {
        try 
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PonteDB>();
                
                // GARANTIA: Cria as tabelas caso ainda não existam no Postgres
                db.Database.EnsureCreated();

                var limite = DateTime.Now.AddDays(-2);
                var expiradas = await db.AnalisesIA
                    .Where(a => a.DataAnalise < limite)
                    .ToListAsync();

                if (expiradas.Any())
                {
                    db.AnalisesIA.RemoveRange(expiradas);
                    await db.SaveChangesAsync();
                    Console.WriteLine($"[LIMPEZA] {expiradas.Count} registros removidos.");
                }
            }
        }
        catch (Exception ex)
        {
            // Evita que um erro de banco derrube o app inteiro
            Console.WriteLine($"[ERRO LIMPEZA] Tabela pode não estar pronta: {ex.Message}");
        }

        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
    }
}
}