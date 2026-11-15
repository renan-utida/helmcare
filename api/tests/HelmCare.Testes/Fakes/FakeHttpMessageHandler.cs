using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Handler HTTP em memória, sem rede: devolve a resposta configurada, ou lança a exceção
/// configurada, e guarda cada requisição recebida (método, endereço e corpo).
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<RequisicaoRecebidaFake> _requisicoes = [];
    private HttpStatusCode _status = HttpStatusCode.OK;
    private string _corpo = "{}";
    private string _tipoConteudo = "application/json";
    private Func<Exception>? _excecao;

    /// <summary>
    /// Requisições recebidas, na ordem.
    /// </summary>
    public IReadOnlyList<RequisicaoRecebidaFake> Requisicoes => _requisicoes;

    /// <summary>
    /// Passa a responder com o status e o corpo informados.
    /// </summary>
    /// <param name="status">Status da resposta.</param>
    /// <param name="corpo">Corpo da resposta.</param>
    /// <param name="tipoConteudo">Content-Type da resposta.</param>
    public void Responder(HttpStatusCode status, string corpo, string tipoConteudo = "application/json")
    {
        _status = status;
        _corpo = corpo;
        _tipoConteudo = tipoConteudo;
        _excecao = null;
    }

    /// <summary>
    /// Passa a lançar <see cref="HttpRequestException"/>, como numa conexão recusada.
    /// </summary>
    public void LancarErroDeRede()
    {
        _excecao = () => new HttpRequestException("Conexão recusada (simulada).");
    }

    /// <summary>
    /// Passa a lançar <see cref="TaskCanceledException"/> com uma <see cref="TimeoutException"/>
    /// interna, que é como o <see cref="HttpClient"/> sinaliza o tempo esgotado.
    /// </summary>
    public void LancarTempoEsgotado()
    {
        _excecao = () => new TaskCanceledException("Tempo esgotado (simulado).", new TimeoutException());
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        _requisicoes.Add(new RequisicaoRecebidaFake(request.Method, request.RequestUri, corpo));

        // Quem chamou cancelou: o HttpClient transforma isto no cancelamento do token dele.
        cancellationToken.ThrowIfCancellationRequested();

        if (_excecao is not null)
        {
            throw _excecao();
        }

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_corpo, Encoding.UTF8, _tipoConteudo),
        };
    }
}

/// <summary>
/// Uma requisição recebida pelo <see cref="FakeHttpMessageHandler"/>.
/// </summary>
/// <param name="Metodo">Método HTTP.</param>
/// <param name="Endereco">Endereço completo.</param>
/// <param name="Corpo">Corpo, ou nulo sem corpo.</param>
public sealed record RequisicaoRecebidaFake(HttpMethod Metodo, Uri? Endereco, string? Corpo);
