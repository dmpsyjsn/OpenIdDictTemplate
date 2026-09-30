namespace OpenIdDictTemplate.Security.Logic.Abstractions;

public interface ICommand;

public interface ICommand<TResult>;

public interface IQuery<TResult>;

public interface IHandleCommandAsync<in TCommand> where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface IHandleCommandAsync<in TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface IHandleQueryAsync<in TQuery, TResult> where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
