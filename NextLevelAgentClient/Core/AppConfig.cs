using System.Text.Json;

namespace NextLevelAgentClient.Core
{
    /// <summary>
    /// Configuração da aplicação (URL base do backend e ambiente), lida de appsettings.json
    /// na pasta de execução. Enquanto não há backend real, essas variáveis servem para
    /// preparar/simular a integração.
    /// </summary>
    public sealed class AppConfig
    {
        public AppEnvironment Environment { get; init; } = AppEnvironment.Dev;
        public string BackendBaseUrl { get; init; } = string.Empty;

        private static AppConfig? _current;
        public static AppConfig Current => _current ??= Load();

        private static AppConfig Load()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

            try
            {
                string json = File.ReadAllText(path);
                RawConfig? raw = JsonSerializer.Deserialize<RawConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return new AppConfig
                {
                    Environment = ParseEnvironment(raw?.Environment),
                    BackendBaseUrl = raw?.BackendBaseUrl ?? string.Empty,
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao carregar appsettings.json: {ex.Message}. Usando configuração padrão (DEV).");
                return new AppConfig();
            }
        }

        private static AppEnvironment ParseEnvironment(string? value) => value?.Trim().ToUpperInvariant() switch
        {
            "PROD" => AppEnvironment.Prod,
            _ => AppEnvironment.Dev,
        };

        private sealed class RawConfig
        {
            public string? Environment { get; set; }
            public string? BackendBaseUrl { get; set; }
        }
    }
}
