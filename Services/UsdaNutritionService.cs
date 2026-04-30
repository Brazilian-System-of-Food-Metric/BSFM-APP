using System.Net.Http.Json;

namespace BSFM.Services
{
    public class UsdaNutritionService
    {
        private readonly HttpClient _httpClient;
        private const string UsdaUrl = "https://api.nal.usda.gov/fdc/v1/foods/search";
        private readonly string _apiKey;

        public UsdaNutritionService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _apiKey = Environment.GetEnvironmentVariable("USDA_API_KEY") ?? "api_nao_encontrada";
        }

        public async Task<NutrientesDTO?> BuscarNutrientes(string queryAlimento)
        {
            // Tradução simples de Label -> Query (Estratégia inicial)
            // No futuro, usar um Dicionário de Tradução EN-US para busca técnica
            var endpoint = $"{UsdaUrl}?api_key={_apiKey}&query={queryAlimento}&pageSize=1&dataType=Foundation,SR%20Legacy";

            var response = await _httpClient.GetAsync(endpoint);
            if (!response.IsSuccessStatusCode) return null;

            var data = await response.Content.ReadFromJsonAsync<UsdaResponse>();
            var food = data?.Foods?.FirstOrDefault();

            if (food == null) return null;

            // Extração segura de macronutrientes por 100g
            return new NutrientesDTO
        {
            NomeOriginal = food.Description,
            Calorias100g = food.FoodNutrients?.FirstOrDefault(n => n.NutrientId == 1008)?.Value ?? 0,
            Proteinas100g = food.FoodNutrients?.FirstOrDefault(n => n.NutrientId == 1003)?.Value ?? 0,
            Carbos100g = food.FoodNutrients?.FirstOrDefault(n => n.NutrientId == 1005)?.Value ?? 0,
            Gorduras100g = food.FoodNutrients?.FirstOrDefault(n => n.NutrientId == 1004)?.Value ?? 0
        };
        }
    }

    // Classes auxiliares para mapear o JSON complexo da USDA
    public class NutrientesDTO {
        public string NomeOriginal { get; set; } = string.Empty; // Iniciamos vazio para evitar aviso
        public double Calorias100g { get; set; }
        public double Proteinas100g { get; set; }
        public double Carbos100g { get; set; }
        public double Gorduras100g { get; set; }
    }

    public class UsdaResponse {
        public List<UsdaFoodItem>? Foods { get; set; } // O ponto de interrogação diz que pode ser nulo
    }

    public class UsdaFoodItem {
        public string Description { get; set; } = string.Empty;
        public List<UsdaNutrient>? FoodNutrients { get; set; }
    }

    public class UsdaNutrient {
        public int NutrientId { get; set; }
        public double Value { get; set; }
    }
}