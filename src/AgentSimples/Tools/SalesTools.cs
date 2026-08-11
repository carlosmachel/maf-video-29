using System.ComponentModel;

namespace AgentSimples.Tools;

/// <summary>
/// Dados fake de vendas usados nas 3 demos, para manter a apresentacao
/// determinística e independente de rede/API externas.
/// </summary>
public static class SalesTools
{
    private static readonly Dictionary<string, decimal> Revenue = new()
    {
        ["2025-09"] = 182_400m,
        ["2025-10"] = 214_900m,
        ["2025-11"] = 198_650m,
    };

    private static readonly Dictionary<string, string[]> TopProducts = new()
    {
        ["2025-09"] = ["Plano Pro", "Plano Starter", "Add-on Analytics"],
        ["2025-10"] = ["Plano Pro", "Add-on Analytics", "Plano Enterprise"],
        ["2025-11"] = ["Plano Pro", "Plano Starter", "Plano Enterprise"],
    };

    [Description("Retorna a receita total em BRL de um mes, no formato yyyy-MM. Ex: 2025-10.")]
    public static decimal GetRevenue([Description("Mes no formato yyyy-MM")] string month)
        => Revenue.TryGetValue(month, out var value)
            ? value
            : throw new ArgumentException($"Sem dados de receita para {month}. Meses disponiveis: {string.Join(", ", Revenue.Keys)}");

    [Description("Retorna os 3 produtos mais vendidos em um mes, no formato yyyy-MM.")]
    public static string[] GetTopProducts([Description("Mes no formato yyyy-MM")] string month)
        => TopProducts.TryGetValue(month, out var value)
            ? value
            : throw new ArgumentException($"Sem dados de produtos para {month}. Meses disponiveis: {string.Join(", ", TopProducts.Keys)}");

    [Description("Compara a receita entre dois meses e retorna a variacao percentual.")]
    public static string CompareRevenue(
        [Description("Mes base, formato yyyy-MM")] string monthA,
        [Description("Mes de comparacao, formato yyyy-MM")] string monthB)
    {
        var a = GetRevenue(monthA);
        var b = GetRevenue(monthB);
        var delta = (b - a) / a * 100m;
        return $"Receita de {monthA}: R$ {a:N2} | Receita de {monthB}: R$ {b:N2} | Variacao: {delta:N1}%";
    }
}
