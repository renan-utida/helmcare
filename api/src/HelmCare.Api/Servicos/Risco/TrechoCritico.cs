namespace HelmCare.Api.Servicos.Risco;

/// <summary>
/// Um trecho com sinistros graves, como vem do <c>GET /risco/v1/trechos-criticos</c> (seção 8).
/// </summary>
/// <param name="Posicao">Posição na lista (1 é o trecho com mais sinistros).</param>
/// <param name="Logradouro">Nome do logradouro.</param>
/// <param name="Latitude">Latitude do trecho, em graus decimais.</param>
/// <param name="Longitude">Longitude do trecho, em graus decimais.</param>
/// <param name="QuantidadeSinistrosGraves">Sinistros graves no período.</param>
public sealed record TrechoCritico(
    int Posicao,
    string Logradouro,
    double Latitude,
    double Longitude,
    int QuantidadeSinistrosGraves);
