using System;
using HelmCare.Dominio.Comum;

namespace HelmCare.Dominio.Entidades;

/// <summary>
/// Registro de que um voluntário aceitou socorrer uma ocorrência. Quem cria é a
/// <c>Ocorrencia</c>, na transição para <c>SOCORRO_A_CAMINHO</c>.
/// </summary>
public sealed class AceiteSocorro : Entidade
{
    /// <summary>
    /// Cria o aceite. Interno: só a <c>Ocorrencia</c> chama.
    /// </summary>
    /// <param name="ocorrenciaId">Ocorrência aceita.</param>
    /// <param name="voluntarioId">Voluntário que aceitou.</param>
    /// <param name="aceitoEm">Instante do aceite, em UTC.</param>
    internal AceiteSocorro(Guid ocorrenciaId, Guid voluntarioId, DateTime aceitoEm)
    {
        OcorrenciaId = Garantir.IdInformado(ocorrenciaId, "ocorrenciaId");
        VoluntarioId = Garantir.IdInformado(voluntarioId, "voluntarioId");
        AceitoEm = Garantir.Utc(aceitoEm, "aceitoEm");
    }

    // Construtor usado só pelo EF Core ao materializar o registro do banco.
    private AceiteSocorro()
    {
    }

    /// <summary>
    /// Ocorrência aceita.
    /// </summary>
    public Guid OcorrenciaId { get; private set; }

    /// <summary>
    /// Voluntário que aceitou.
    /// </summary>
    public Guid VoluntarioId { get; private set; }

    /// <summary>
    /// Instante do aceite, em UTC.
    /// </summary>
    public DateTime AceitoEm { get; private set; }
}
