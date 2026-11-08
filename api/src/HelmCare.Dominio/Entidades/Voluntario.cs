using System;
using HelmCare.Dominio.Comum;
using HelmCare.Dominio.Enums;
using HelmCare.Dominio.ObjetosDeValor;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Participante da rede que recebe e aceita chamados de SOS. Todo voluntário também é um
/// entregador cadastrado (seção 3 do contrato). A posição é localização pessoal (seção 9).
/// </summary>
public sealed class Voluntario : Entidade
{
    /// <summary>
    /// Cadastra o voluntário indisponível e sem posição; ele entra na rede ao ficar disponível.
    /// </summary>
    /// <param name="entregadorId">Entregador que se ofereceu como voluntário.</param>
    /// <param name="tipo">Tipo de voluntário.</param>
    /// <exception cref="ValidacaoDominioException">Quando algum dado é inválido.</exception>
    public Voluntario(Guid entregadorId, TipoVoluntario tipo)
    {
        EntregadorId = Garantir.IdInformado(entregadorId, "entregadorId");
        Tipo = Garantir.Definido(tipo, "tipo");
        Disponivel = false;
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private Voluntario()
    {
    }

    /// <summary>
    /// Entregador que é este voluntário.
    /// </summary>
    public Guid EntregadorId { get; private set; }

    /// <summary>
    /// Tipo de voluntário.
    /// </summary>
    public TipoVoluntario Tipo { get; private set; }

    /// <summary>
    /// Indica se o voluntário está recebendo chamados agora.
    /// </summary>
    public bool Disponivel { get; private set; }

    /// <summary>
    /// Latitude da última posição informada, ou nulo quando indisponível.
    /// </summary>
    public double? Latitude { get; private set; }

    /// <summary>
    /// Longitude da última posição informada, ou nulo quando indisponível.
    /// </summary>
    public double? Longitude { get; private set; }

    /// <summary>
    /// Liga ou desliga o recebimento de chamados.
    /// </summary>
    /// <param name="disponivel">Se o voluntário passa a receber chamados.</param>
    /// <param name="posicao">Posição atual, obrigatória quando disponível.</param>
    /// <exception cref="ValidacaoDominioException">Quando fica disponível sem informar a posição.</exception>
    public void AtualizarDisponibilidade(bool disponivel, Coordenada? posicao)
    {
        if (disponivel && posicao is null)
        {
            throw new ValidacaoDominioException("A posição é obrigatória para ficar disponível.");
        }

        Disponivel = disponivel;

        // Privacidade por padrão: indisponível, a API não precisa saber onde a pessoa está,
        // então a última posição é apagada em vez de ficar guardada.
        Latitude = disponivel ? posicao!.Value.Latitude : null;
        Longitude = disponivel ? posicao!.Value.Longitude : null;
    }
}
