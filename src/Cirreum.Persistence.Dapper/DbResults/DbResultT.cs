namespace Cirreum.Persistence;

using Cirreum;
using System.Runtime.CompilerServices;

/// <summary>
/// Represents a database operation result that carries a <see cref="TransactionContext"/> context
/// for fluent chaining of database operations within a transaction.
/// </summary>
/// <remarks>
/// <para>
/// This type enables fluent chaining without passing the builder to each method:
/// </para>
/// <code>
/// return await conn.ExecuteInTransactionAsync(builder =&gt; builder
///     .GetAsync&lt;Data&gt;(sql, key)
///     .EnsureAsync(d =&gt; d.IsActive, new BadRequestException("Not active"))
///     .ThenGetAsync&lt;int&gt;(countSql, parameters, countKey)
///     .ThenDeleteAsync(deleteSql, parameters, deleteKey)
///     .ThenGetAsync&lt;Data&gt;(nextItemSql)
/// ), cancellationToken);
/// </code>
/// </remarks>
/// <typeparam name="T">The type of the value in the result.</typeparam>
public readonly struct DbResult<T>(TransactionContext builder, Task<Result<T>> resultTask) {

	/// <summary>
	/// Gets the underlying result task.
	/// </summary>
	public Task<Result<T>> Result => resultTask;

	/// <summary>
	/// Gets the transaction builder context.
	/// </summary>
	internal TransactionContext Builder => builder;

	/// <summary>
	/// Enables direct awaiting of the DbResult.
	/// </summary>
	public TaskAwaiter<Result<T>> GetAwaiter() => resultTask.GetAwaiter();

	/// <summary>
	/// Implicitly converts a DbResult to its underlying Task for compatibility with existing Result extensions.
	/// </summary>
	public static implicit operator Task<Result<T>>(DbResult<T> dbResult) => dbResult.Result;

	#region Ensure

	/// <summary>
	/// Validates the result value using a predicate. If the predicate returns false, fails with the provided error.
	/// </summary>
	public DbResult<T> EnsureAsync(Func<T, bool> predicate, Exception error)
		=> new(builder, resultTask.WhereAsyncTask(predicate, error));

	/// <summary>
	/// Validates the result value using an async predicate. If the predicate returns false, fails with the provided error.
	/// </summary>
	public DbResult<T> EnsureAsync(Func<T, Task<bool>> predicate, Exception error)
		=> new(builder, resultTask.WhereAsyncTask(predicate, error));

	#endregion

	#region Map

	/// <summary>
	/// Maps the result value to a new type.
	/// </summary>
	public DbResult<TResult> MapAsync<TResult>(Func<T, TResult> mapper)
		=> new(builder, resultTask.MapAsyncTask(mapper));

	/// <summary>
	/// Maps the result value to a new type using an async function.
	/// </summary>
	public DbResult<TResult> MapAsync<TResult>(Func<T, Task<TResult>> mapper)
		=> new(builder, resultTask.MapAsyncTask(mapper));

	#endregion

	#region Then

	/// <summary>
	/// Chains an arbitrary async operation that returns a non-generic Result.
	/// Use this as an escape hatch to integrate external async operations into the fluent chain.
	/// </summary>
	/// <param name="next">The async operation to execute, receiving the current value.</param>
	public DbResult ThenAsync(Func<T, Task<Result>> next)
		=> new(builder, resultTask.ThenAsyncTask(next));

	/// <summary>
	/// Chains an arbitrary async operation that returns a Result&lt;TResult&gt;.
	/// Use this as an escape hatch to integrate external async operations into the fluent chain.
	/// </summary>
	/// <param name="next">The async operation to execute, receiving the current value.</param>
	public DbResult<TResult> ThenAsync<TResult>(Func<T, Task<Result<TResult>>> next)
		=> new(builder, this.ThenAsyncCore(next));

	private async Task<Result<TResult>> ThenAsyncCore<TResult>(Func<T, Task<Result<TResult>>> next) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		return await next(result.Value).ConfigureAwait(false);
	}

	#endregion

	#region Insert

	/// <summary>
	/// Chains an INSERT operation after a successful result.
	/// </summary>
	public DbResult ThenInsertAsync(
		string sql,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation after a successful result.
	/// </summary>
	public DbResult ThenInsertAsync(
		string sql,
		object parameters,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, parameters, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation after a successful result, using the previous value to build parameters.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenInsertAsync(
		string sql,
		Func<T, object?> parametersFactory,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCoreNonGeneric(sql, parametersFactory, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation that returns a value after a successful result.
	/// </summary>
	public DbResult<TResult> ThenInsertAsync<TResult>(
		string sql,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation that returns a value after a successful result.
	/// </summary>
	public DbResult<TResult> ThenInsertAsync<TResult>(
		string sql,
		object parameters,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, parameters, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation that returns a value after a successful result, using the previous value to build parameters.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="resultSelector">Factory to create the result value after successful insert.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<TResult> ThenInsertAsync<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCoreWithResultSelector(sql, parametersFactory, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an INSERT operation after a successful result.
	/// If <paramref name="when"/> returns false, the insert is skipped and the chain continues with the current value.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="when">Predicate that determines whether to execute the insert; if false, the insert is skipped and the current value passes through.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenInsertIfAsync(
		string sql,
		Func<T, bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertIfAsyncCoreNoParams(sql, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an INSERT operation after a successful result.
	/// If <paramref name="when"/> returns false, the insert is skipped and the chain continues with the current value.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parameters">The parameters for the INSERT statement.</param>
	/// <param name="when">Predicate that determines whether to execute the insert; if false, the insert is skipped and the current value passes through.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenInsertIfAsync(
		string sql,
		object parameters,
		Func<T, bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertIfAsyncCoreWithParams(sql, parameters, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an INSERT operation after a successful result, using the previous value to build parameters.
	/// If <paramref name="when"/> returns false, the insert is skipped and the chain continues with the current value.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="when">Predicate that determines whether to execute the insert; if false, the insert is skipped and the current value passes through.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenInsertIfAsync(
		string sql,
		Func<T, object?> parametersFactory,
		Func<T, bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertIfAsyncCore(sql, parametersFactory, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an INSERT operation that returns a value after a successful result, using the previous value to build parameters.
	/// If <paramref name="when"/> returns false, the insert is skipped and the chain continues with the result from <paramref name="resultSelector"/>.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="resultSelector">Factory to create the result value from the current value, called after successful insert or when skipped.</param>
	/// <param name="when">Predicate that determines whether to execute the insert; if false, the insert is skipped.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<TResult> ThenInsertIfAsync<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		Func<T, TResult> resultSelector,
		Func<T, bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertIfAsyncCore(sql, parametersFactory, resultSelector, when, uniqueConstraintMessage, foreignKeyMessage));


	private async Task<Result> ThenInsertAsyncCore(
		string sql,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.ToResult();
		}
		return await builder.InsertAsync(sql, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenInsertAsyncCore(
		string sql,
		object parameters,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.ToResult();
		}
		return await builder.InsertAsync(sql, parameters, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenInsertAsyncCoreNonGeneric(
		string sql,
		Func<T, object?> parametersFactory,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.ToResult();
		}
		return await builder.InsertAsync(sql, parametersFactory(result.Value), uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenInsertIfAsyncCoreNoParams(
		string sql,
		Func<T, bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when(result.Value)) {
			return result;
		}
		var insertResult = await builder.InsertAsync(sql, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (insertResult.IsFailure) {
			return insertResult.Error;
		}
		return result;
	}

	private async Task<Result<T>> ThenInsertIfAsyncCoreWithParams(
		string sql,
		object parameters,
		Func<T, bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when(result.Value)) {
			return result;
		}
		var insertResult = await builder.InsertAsync(sql, parameters, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (insertResult.IsFailure) {
			return insertResult.Error;
		}
		return result;
	}

	private async Task<Result<T>> ThenInsertIfAsyncCore(
		string sql,
		Func<T, object?> parametersFactory,
		Func<T, bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when(result.Value)) {
			return result;
		}
		var insertResult = await builder.InsertAsync(sql, parametersFactory(result.Value), uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (insertResult.IsFailure) {
			return insertResult.Error;
		}
		return result;
	}

	private async Task<Result<TResult>> ThenInsertIfAsyncCore<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		Func<T, TResult> resultSelector,
		Func<T, bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		if (!when(result.Value)) {
			return resultSelector(result.Value);
		}
		var insertResult = await builder.InsertAsync(sql, parametersFactory(result.Value), uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (insertResult.IsFailure) {
			return insertResult.Error;
		}
		return resultSelector(result.Value);
	}

	private async Task<Result<TResult>> ThenInsertAsyncCore<TResult>(
		string sql,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		return await builder.InsertAsync(sql, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenInsertAsyncCore<TResult>(
		string sql,
		object parameters,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		return await builder.InsertAsync(sql, parameters, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenInsertAsyncCoreWithResultSelector<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		var insertResult = await builder.InsertAsync(sql, parametersFactory(result.Value), uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (insertResult.IsFailure) {
			return insertResult.Error;
		}
		return resultSelector();
	}

	#endregion

	#region Update

	/// <summary>
	/// Chains an UPDATE operation after a successful result.
	/// </summary>
	public DbResult ThenUpdateAsync(
		string sql,
		object? parameters,
		object key,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCore(sql, parameters, key, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an UPDATE operation after a successful result, using the previous value to build parameters.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenUpdateAsync(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCoreNonGeneric(sql, parametersFactory, key, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an UPDATE operation that returns a value after a successful result.
	/// </summary>
	public DbResult<TResult> ThenUpdateAsync<TResult>(
		string sql,
		object? parameters,
		object key,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCore(sql, parameters, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an UPDATE operation that returns a value after a successful result, using the previous value to build parameters.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="resultSelector">Factory to create the result value.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<TResult> ThenUpdateAsync<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCoreWithResultSelector(sql, parametersFactory, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an UPDATE operation after a successful result.
	/// If <paramref name="when"/> returns false, the update is skipped and the chain continues with the current value.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parameters">The parameters for the UPDATE statement.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="when">Predicate that determines whether to execute the update; if false, the update is skipped and the current value passes through.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenUpdateIfAsync(
		string sql,
		object? parameters,
		object key,
		Func<T, bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateIfAsyncCoreWithParams(sql, parameters, key, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an UPDATE operation after a successful result, using the previous value to build parameters.
	/// If <paramref name="when"/> returns false, the update is skipped and the chain continues with the current value.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="when">Predicate that determines whether to execute the update; if false, the update is skipped and the current value passes through.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenUpdateIfAsync(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<T, bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateIfAsyncCore(sql, parametersFactory, key, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an UPDATE operation that returns a value after a successful result, using the previous value to build parameters.
	/// If <paramref name="when"/> returns false, the update is skipped and the chain continues with the result from <paramref name="resultSelector"/>.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="resultSelector">Factory to create the result value from the current value, called after successful update or when skipped.</param>
	/// <param name="when">Predicate that determines whether to execute the update; if false, the update is skipped.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<TResult> ThenUpdateIfAsync<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<T, TResult> resultSelector,
		Func<T, bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateIfAsyncCore(sql, parametersFactory, key, resultSelector, when, uniqueConstraintMessage, foreignKeyMessage));

	private async Task<Result> ThenUpdateAsyncCore(
		string sql,
		object? parameters,
		object key,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.ToResult();
		}
		return await builder.UpdateAsync(sql, parameters, key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenUpdateAsyncCoreNonGeneric(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.ToResult();
		}
		return await builder.UpdateAsync(sql, parametersFactory(result.Value), key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenUpdateAsyncCore<TResult>(
		string sql,
		object? parameters,
		object key,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		return await builder.UpdateAsync(sql, parameters, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenUpdateAsyncCoreWithResultSelector<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		var updateResult = await builder.UpdateAsync(sql, parametersFactory(result.Value), key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (updateResult.IsFailure) {
			return updateResult.Error;
		}
		return resultSelector();
	}

	private async Task<Result<T>> ThenUpdateIfAsyncCoreWithParams(
		string sql,
		object? parameters,
		object key,
		Func<T, bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when(result.Value)) {
			return result;
		}
		var updateResult = await builder.UpdateAsync(sql, parameters, key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (updateResult.IsFailure) {
			return updateResult.Error;
		}
		return result;
	}

	private async Task<Result<T>> ThenUpdateIfAsyncCore(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<T, bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when(result.Value)) {
			return result;
		}
		var updateResult = await builder.UpdateAsync(sql, parametersFactory(result.Value), key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (updateResult.IsFailure) {
			return updateResult.Error;
		}
		return result;
	}

	private async Task<Result<TResult>> ThenUpdateIfAsyncCore<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<T, TResult> resultSelector,
		Func<T, bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		if (!when(result.Value)) {
			return resultSelector(result.Value);
		}
		var updateResult = await builder.UpdateAsync(sql, parametersFactory(result.Value), key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		if (updateResult.IsFailure) {
			return updateResult.Error;
		}
		return resultSelector(result.Value);
	}

	#endregion

	#region Delete

	/// <summary>
	/// Chains a DELETE operation after a successful result.
	/// </summary>
	public DbResult ThenDeleteAsync(
		string sql,
		object? parameters,
		object key,
		string foreignKeyMessage = "Cannot delete, record is in use")
		=> new(builder, this.ThenDeleteAsyncCore(sql, parameters, key, foreignKeyMessage));

	/// <summary>
	/// Chains a DELETE operation after a successful result, using the previous value to build parameters.
	/// </summary>
	/// <param name="sql">The DELETE SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenDeleteAsync(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		string foreignKeyMessage = "Cannot delete, record is in use")
		=> new(builder, this.ThenDeleteAsyncCoreNonGeneric(sql, parametersFactory, key, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains a DELETE operation after a successful result.
	/// If <paramref name="when"/> returns false, the delete is skipped and the chain continues with the current value.
	/// </summary>
	/// <param name="sql">The DELETE SQL statement.</param>
	/// <param name="parameters">The parameters for the DELETE statement.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="when">Predicate that determines whether to execute the delete; if false, the delete is skipped and the current value passes through.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenDeleteIfAsync(
		string sql,
		object? parameters,
		object key,
		Func<T, bool> when,
		string foreignKeyMessage = "Cannot delete, record is in use")
		=> new(builder, this.ThenDeleteIfAsyncCoreWithParams(sql, parameters, key, when, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains a DELETE operation after a successful result, using the previous value to build parameters.
	/// If <paramref name="when"/> returns false, the delete is skipped and the chain continues with the current value.
	/// </summary>
	/// <param name="sql">The DELETE SQL statement.</param>
	/// <param name="parametersFactory">Factory to create parameters from the current value.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="when">Predicate that determines whether to execute the delete; if false, the delete is skipped and the current value passes through.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenDeleteIfAsync(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<T, bool> when,
		string foreignKeyMessage = "Cannot delete, record is in use")
		=> new(builder, this.ThenDeleteIfAsyncCore(sql, parametersFactory, key, when, foreignKeyMessage));

	private async Task<Result> ThenDeleteAsyncCore(
		string sql,
		object? parameters,
		object key,
		string foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.ToResult();
		}
		return await builder.DeleteAsync(sql, parameters, key, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenDeleteAsyncCoreNonGeneric(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		string foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.ToResult();
		}
		return await builder.DeleteAsync(sql, parametersFactory(result.Value), key, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenDeleteIfAsyncCoreWithParams(
		string sql,
		object? parameters,
		object key,
		Func<T, bool> when,
		string foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when(result.Value)) {
			return result;
		}
		var deleteResult = await builder.DeleteAsync(sql, parameters, key, foreignKeyMessage).Result.ConfigureAwait(false);
		if (deleteResult.IsFailure) {
			return deleteResult.Error;
		}
		return result;
	}

	private async Task<Result<T>> ThenDeleteIfAsyncCore(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<T, bool> when,
		string foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when(result.Value)) {
			return result;
		}
		var deleteResult = await builder.DeleteAsync(sql, parametersFactory(result.Value), key, foreignKeyMessage).Result.ConfigureAwait(false);
		if (deleteResult.IsFailure) {
			return deleteResult.Error;
		}
		return result;
	}

	#endregion

	#region Get

	/// <summary>
	/// Chains a GET operation after a successful result.
	/// </summary>
	public DbResult<TResult> ThenGetAsync<TResult>(string sql, object key)
		=> new(builder, this.ThenGetAsyncCore<TResult>(sql, null, key));

	/// <summary>
	/// Chains a GET operation with parameters after a successful result.
	/// </summary>
	public DbResult<TResult> ThenGetAsync<TResult>(string sql, object? parameters, object key)
		=> new(builder, this.ThenGetAsyncCore<TResult>(sql, parameters, key));

	/// <summary>
	/// Chains a GET operation after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<TResult> ThenGetAsync<TResult>(string sql, Func<T, object?> parametersFactory, object key)
		=> new(builder, this.ThenGetAsyncCoreWithFactory<TResult>(sql, parametersFactory, key));

	/// <summary>
	/// Chains a GET operation with mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetAsync<TData, TModel>(string sql, object key, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetAsyncCoreWithMapper(sql, null, key, mapper));

	/// <summary>
	/// Chains a GET operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetAsync<TData, TModel>(string sql, object? parameters, object key, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetAsyncCoreWithMapper(sql, parameters, key, mapper));

	/// <summary>
	/// Chains a GET operation with mapping after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<TModel> ThenGetAsync<TData, TModel>(string sql, Func<T, object?> parametersFactory, object key, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetAsyncCoreWithMapperAndFactory(sql, parametersFactory, key, mapper));

	private async Task<Result<TResult>> ThenGetAsyncCore<TResult>(string sql, object? parameters, object key) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetAsync<TResult>(sql, parameters, key).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenGetAsyncCoreWithFactory<TResult>(string sql, Func<T, object?> parametersFactory, object key) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetAsync<TResult>(sql, parametersFactory(result.Value), key).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetAsyncCoreWithMapper<TData, TModel>(string sql, object? parameters, object key, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetAsync(sql, parameters, key, mapper).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetAsyncCoreWithMapperAndFactory<TData, TModel>(string sql, Func<T, object?> parametersFactory, object key, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetAsync(sql, parametersFactory(result.Value), key, mapper).Result.ConfigureAwait(false);
	}

	#endregion

	#region GetScalar

	/// <summary>
	/// Chains a GET scalar operation after a successful result.
	/// </summary>
	public DbResult<TResult> ThenGetScalarAsync<TResult>(string sql)
		=> new(builder, this.ThenGetScalarAsyncCore<TResult>(sql, null));

	/// <summary>
	/// Chains a GET scalar operation with parameters after a successful result.
	/// </summary>
	public DbResult<TResult> ThenGetScalarAsync<TResult>(string sql, object? parameters)
		=> new(builder, this.ThenGetScalarAsyncCore<TResult>(sql, parameters));

	/// <summary>
	/// Chains a GET scalar operation after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<TResult> ThenGetScalarAsync<TResult>(string sql, Func<T, object?> parametersFactory)
		=> new(builder, this.ThenGetScalarAsyncCoreWithFactory<TResult>(sql, parametersFactory));

	/// <summary>
	/// Chains a GET scalar operation with mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetScalarAsync<TData, TModel>(string sql, Func<TData?, TModel> mapper)
		=> new(builder, this.ThenGetScalarAsyncCoreWithMapper(sql, null, mapper));

	/// <summary>
	/// Chains a GET scalar operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetScalarAsync<TData, TModel>(string sql, object? parameters, Func<TData?, TModel> mapper)
		=> new(builder, this.ThenGetScalarAsyncCoreWithMapper(sql, parameters, mapper));

	/// <summary>
	/// Chains a GET scalar operation with mapping after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<TModel> ThenGetScalarAsync<TData, TModel>(string sql, Func<T, object?> parametersFactory, Func<TData?, TModel> mapper)
		=> new(builder, this.ThenGetScalarAsyncCoreWithMapperAndFactory(sql, parametersFactory, mapper));

	private async Task<Result<TResult>> ThenGetScalarAsyncCore<TResult>(string sql, object? parameters) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetScalarAsync<TResult>(sql, parameters).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenGetScalarAsyncCoreWithFactory<TResult>(string sql, Func<T, object?> parametersFactory) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetScalarAsync<TResult>(sql, parametersFactory(result.Value)).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetScalarAsyncCoreWithMapper<TData, TModel>(string sql, object? parameters, Func<TData?, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetScalarAsync(sql, parameters, mapper).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetScalarAsyncCoreWithMapperAndFactory<TData, TModel>(string sql, Func<T, object?> parametersFactory, Func<TData?, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetScalarAsync(sql, parametersFactory(result.Value), mapper).Result.ConfigureAwait(false);
	}

	#endregion

	#region QueryAny

	/// <summary>
	/// Chains a QueryAny operation after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TResult>> ThenQueryAnyAsync<TResult>(string sql)
		=> new(builder, this.ThenQueryAnyAsyncCore<TResult>(sql, null));

	/// <summary>
	/// Chains a QueryAny operation after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<IReadOnlyList<TResult>> ThenQueryAnyAsync<TResult>(string sql, Func<T, object?> parametersFactory)
		=> new(builder, this.ThenQueryAnyAsyncCoreWithFactory<TResult>(sql, parametersFactory));

	/// <summary>
	/// Chains a QueryAny operation with parameters after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TResult>> ThenQueryAnyAsync<TResult>(string sql, object? parameters)
		=> new(builder, this.ThenQueryAnyAsyncCore<TResult>(sql, parameters));

	/// <summary>
	/// Chains a QueryAny operation with mapping after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TModel>> ThenQueryAnyAsync<TData, TModel>(string sql, Func<TData, TModel> mapper)
		=> new(builder, this.ThenQueryAnyAsyncCoreWithMapper(sql, null, mapper));

	/// <summary>
	/// Chains a QueryAny operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TModel>> ThenQueryAnyAsync<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper)
		=> new(builder, this.ThenQueryAnyAsyncCoreWithMapper(sql, parameters, mapper));

	/// <summary>
	/// Chains a QueryAny operation with mapping after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<IReadOnlyList<TModel>> ThenQueryAnyAsync<TData, TModel>(string sql, Func<T, object?> parametersFactory, Func<TData, TModel> mapper)
		=> new(builder, this.ThenQueryAnyAsyncCoreWithMapperAndFactory(sql, parametersFactory, mapper));

	private async Task<Result<IReadOnlyList<TResult>>> ThenQueryAnyAsyncCore<TResult>(string sql, object? parameters) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.QueryAnyAsync<TResult>(sql, parameters).Result.ConfigureAwait(false);
	}

	private async Task<Result<IReadOnlyList<TResult>>> ThenQueryAnyAsyncCoreWithFactory<TResult>(string sql, Func<T, object?> parametersFactory) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.QueryAnyAsync<TResult>(sql, parametersFactory(result.Value)).Result.ConfigureAwait(false);
	}

	private async Task<Result<IReadOnlyList<TModel>>> ThenQueryAnyAsyncCoreWithMapper<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.QueryAnyAsync(sql, parameters, mapper).Result.ConfigureAwait(false);
	}

	private async Task<Result<IReadOnlyList<TModel>>> ThenQueryAnyAsyncCoreWithMapperAndFactory<TData, TModel>(string sql, Func<T, object?> parametersFactory, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.QueryAnyAsync(sql, parametersFactory(result.Value), mapper).Result.ConfigureAwait(false);
	}

	#endregion

}
