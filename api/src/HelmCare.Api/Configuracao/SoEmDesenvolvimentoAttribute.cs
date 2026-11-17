using System;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Marca um controller que só existe no ambiente Development. Fora dele, o
/// <see cref="ProvedorControllersPorAmbiente"/> nem o registra: as rotas não são mapeadas (404) e não
/// aparecem no Scalar.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SoEmDesenvolvimentoAttribute : Attribute
{
}
