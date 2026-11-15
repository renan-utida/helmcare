using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace HelmCare.Testes.Fakes;

/// <summary>
/// Logger em memória: guarda o nível e o texto já formatado de cada registro, para o teste
/// conferir se a falha foi aviso ou erro e o que o log leva.
/// </summary>
/// <typeparam name="T">Categoria do log.</typeparam>
public sealed class FakeLogger<T> : ILogger<T>
{
    private readonly List<RegistroLogFake> _registros = [];

    /// <summary>
    /// Registros, na ordem.
    /// </summary>
    public IReadOnlyList<RegistroLogFake> Registros => _registros;

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        _registros.Add(new RegistroLogFake(logLevel, formatter(state, exception)));
    }
}

/// <summary>
/// Um registro guardado pelo <see cref="FakeLogger{T}"/>.
/// </summary>
/// <param name="Nivel">Nível do log.</param>
/// <param name="Mensagem">Texto formatado.</param>
public sealed record RegistroLogFake(LogLevel Nivel, string Mensagem);
