using System;

namespace HelmCare.Dominio.Comum;

/// <summary>
/// Classe base de todas as entidades do domínio HelmCare.
/// Garante que toda entidade nasce com um identificador único do tipo <see cref="Guid"/>.
/// </summary>
public abstract class Entidade
{
    /// <summary>
    /// Inicializa a entidade gerando um novo identificador.
    /// </summary>
    protected Entidade()
    {
        // Guid versão 7 (RFC 9562): os primeiros 48 bits são o instante da criação,
        // então os ids saem ordenados no tempo. Isso mantém o índice da chave primária
        // no PostgreSQL compacto (inserções no fim, sem fragmentar), ao contrário do Guid v4,
        // que é totalmente aleatório.
        Id = Guid.CreateVersion7();
    }

    /// <summary>
    /// Identificador único da entidade, gerado na criação e nunca alterado pelo domínio.
    /// </summary>
    public Guid Id { get; protected set; }
}
