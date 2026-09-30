using Microsoft.Extensions.Logging;
using OpenIdDictTemplate.Security.Logic.Data;

namespace OpenIdDictTemplate.Security.Logic.Abstractions;

public class LoggingCommandDecorator<TCommand>(
    IHandleCommandAsync<TCommand> inner,
    ILogger<LoggingCommandDecorator<TCommand>> logger) : IHandleCommandAsync<TCommand>
    where TCommand : ICommand
{
    public async Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var result = await inner.HandleAsync(command, cancellationToken);
        CqrsLog.LogResult(logger, typeof(TCommand).Name, result);
        return result;
    }
}

public class LoggingCommandDecorator<TCommand, TResult>(
    IHandleCommandAsync<TCommand, TResult> inner,
    ILogger<LoggingCommandDecorator<TCommand, TResult>> logger) : IHandleCommandAsync<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var result = await inner.HandleAsync(command, cancellationToken);
        CqrsLog.LogResult(logger, typeof(TCommand).Name, result);
        return result;
    }
}

public class LoggingQueryDecorator<TQuery, TResult>(
    IHandleQueryAsync<TQuery, TResult> inner,
    ILogger<LoggingQueryDecorator<TQuery, TResult>> logger) : IHandleQueryAsync<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        var result = await inner.HandleAsync(query, cancellationToken);
        CqrsLog.LogResult(logger, typeof(TQuery).Name, result);
        return result;
    }
}

/// <summary>Saves pending changes on the shared DbContext after a successful command.</summary>
public class UnitOfWorkCommandDecorator<TCommand>(
    IHandleCommandAsync<TCommand> inner,
    ApplicationDbContext dbContext) : IHandleCommandAsync<TCommand>
    where TCommand : ICommand
{
    public async Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var result = await inner.HandleAsync(command, cancellationToken);
        if (result.IsSuccess && dbContext.ChangeTracker.HasChanges())
            await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}

public class UnitOfWorkCommandDecorator<TCommand, TResult>(
    IHandleCommandAsync<TCommand, TResult> inner,
    ApplicationDbContext dbContext) : IHandleCommandAsync<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        var result = await inner.HandleAsync(command, cancellationToken);
        if (result.IsSuccess && dbContext.ChangeTracker.HasChanges())
            await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }
}

internal static class CqrsLog
{
    public static void LogResult(ILogger logger, string name, Result result)
    {
        if (result.IsSuccess)
            logger.LogDebug("{Message} succeeded", name);
        else
            logger.LogWarning("{Message} failed: {ErrorType} - {Error}", name, result.Error!.Type, result.Error.Message);
    }
}
