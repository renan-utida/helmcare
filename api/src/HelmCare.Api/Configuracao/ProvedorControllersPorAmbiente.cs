using System;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;

namespace HelmCare.Api.Configuracao;

/// <summary>
/// Descobre os controllers como o provedor padrão do MVC, mas deixa de fora os marcados com
/// <see cref="SoEmDesenvolvimentoAttribute"/> quando o ambiente não é Development.
/// </summary>
public sealed class ProvedorControllersPorAmbiente : ControllerFeatureProvider
{
    private readonly bool _desenvolvimento;

    /// <summary>
    /// Cria o provedor.
    /// </summary>
    /// <param name="desenvolvimento">Verdadeiro no ambiente Development.</param>
    public ProvedorControllersPorAmbiente(bool desenvolvimento)
    {
        _desenvolvimento = desenvolvimento;
    }

    /// <summary>
    /// Registra os controllers da API com este provedor no lugar do padrão.
    /// </summary>
    /// <param name="mvc">Construtor do MVC.</param>
    /// <param name="desenvolvimento">Verdadeiro no ambiente Development.</param>
    /// <returns>O mesmo construtor.</returns>
    public static IMvcBuilder Aplicar(IMvcBuilder mvc, bool desenvolvimento)
    {
        ArgumentNullException.ThrowIfNull(mvc);

        return mvc.ConfigureApplicationPartManager(partes =>
        {
            foreach (ControllerFeatureProvider padrao in partes.FeatureProviders.OfType<ControllerFeatureProvider>().ToList())
            {
                partes.FeatureProviders.Remove(padrao);
            }

            partes.FeatureProviders.Add(new ProvedorControllersPorAmbiente(desenvolvimento));
        });
    }

    /// <inheritdoc/>
    protected override bool IsController(TypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return base.IsController(typeInfo)
            && (_desenvolvimento || !typeInfo.IsDefined(typeof(SoEmDesenvolvimentoAttribute), inherit: false));
    }
}
