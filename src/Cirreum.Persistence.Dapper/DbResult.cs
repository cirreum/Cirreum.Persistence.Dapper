namespace Cirreum.Persistence;

using System.Runtime.CompilerServices;

/// <summary>
/// Represents a non-generic database operation result that carries a <see cref="TransactionContext"/> context
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
/// ), cancellationToken);
/// </code>
/// </remarks>
public readonly struct DbResult(TransactionContext builder, Task<Result> resultTask) {

	/// <summary>
	/// Gets the underlying result task.
	/// </summary>
	public Task<Result> Result => resultTask;

	/// <summary>
	/// Gets the transaction builder context.
	/// </summary>
	internal TransactionContext Builder => builder;

	/// <summary>
	/// Enables direct awaiting of the DbResultNonGeneric.
	/// </summary>
	public TaskAwaiter<Result> GetAwaiter() => resultTask.GetAwaiter();

	/// <summary>
	/// Implicitly converts to Task for compatibility with existing Result extensions.
	/// </summary>
	public static implicit operator Task<Result>(DbResult dbResult) => dbResult.Result;

	#region Then

	/// <summary>
	/// Chains an arbitrary async operation that returns a non-generic Result.
	/// Use this as an escape hatch to integrate external async operations into the fluent chain.
	/// </summary>
	/// <param name="next">The async operation to execute.</param>
	public DbResult ThenAsync(Func<Task<Result>> next)
		=> new(builder, ThenAsyncCore(next));

	/// <summary>
	/// Chains an arbitrary async operation that returns a Result&lt;T&gt;.
	/// Use this as an escape hatch to integrate external async operations into the fluent chain.
	/// </summary>
	/// <param name="next">The async operation to execute.</param>
	public DbResult<T> ThenAsync<T>(Func<Task<Result<T>>> next)
		=> new(builder, ThenAsyncCore(next));

	private async Task<Result> ThenAsyncCore(Func<Task<Result>> next) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		return await next().ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenAsyncCore<T>(Func<Task<Result<T>>> next) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		return await next().ConfigureAwait(false);
	}

	#endregion

	#region Insert

	/// <summary>
	/// Chains an INSERT operation after a successful result.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenInsertAsync(
		string sql,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation after a successful result.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parameters">The parameters for the INSERT statement.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenInsertAsync(
		string sql,
		object parameters,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, parameters, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation that returns a value after a successful result.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="resultSelector">Factory to create the result value after successful insert.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenInsertAsync<T>(
		string sql,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation that returns a value after a successful result.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parameters">The parameters for the INSERT statement.</param>
	/// <param name="resultSelector">Factory to create the result value after successful insert.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenInsertAsync<T>(
		string sql,
		object parameters,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, parameters, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an INSERT operation after a successful result.
	/// If <paramref name="when"/> returns false, the insert is skipped and the chain continues.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="when">Predicate that determines whether to execute the insert.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenInsertIfAsync(
		string sql,
		Func<bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertIfAsyncCore(sql, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an INSERT operation after a successful result.
	/// If <paramref name="when"/> returns false, the insert is skipped and the chain continues.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parameters">The parameters for the INSERT statement.</param>
	/// <param name="when">Predicate that determines whether to execute the insert.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenInsertIfAsync(
		string sql,
		object parameters,
		Func<bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertIfAsyncCore(sql, parameters, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an INSERT operation that returns a value after a successful result.
	/// If <paramref name="when"/> returns false, the insert is skipped and the chain continues with the result from <paramref name="resultSelector"/>.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="resultSelector">Factory to create the result value after successful insert or when skipped.</param>
	/// <param name="when">Predicate that determines whether to execute the insert.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenInsertIfAsync<T>(
		string sql,
		Func<T> resultSelector,
		Func<bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertIfAsyncCore(sql, resultSelector, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an INSERT operation that returns a value after a successful result.
	/// If <paramref name="when"/> returns false, the insert is skipped and the chain continues with the result from <paramref name="resultSelector"/>.
	/// </summary>
	/// <param name="sql">The INSERT SQL statement.</param>
	/// <param name="parameters">The parameters for the INSERT statement.</param>
	/// <param name="resultSelector">Factory to create the result value after successful insert or when skipped.</param>
	/// <param name="when">Predicate that determines whether to execute the insert.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenInsertIfAsync<T>(
		string sql,
		object parameters,
		Func<T> resultSelector,
		Func<bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertIfAsyncCore(sql, parameters, resultSelector, when, uniqueConstraintMessage, foreignKeyMessage));

	private async Task<Result> ThenInsertAsyncCore(
		string sql,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
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
			return result;
		}
		return await builder.InsertAsync(sql, parameters, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenInsertAsyncCore<T>(
		string sql,
		Func<T> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		return await builder.InsertAsync(sql, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenInsertAsyncCore<T>(
		string sql,
		object parameters,
		Func<T> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		return await builder.InsertAsync(sql, parameters, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenInsertIfAsyncCore(
		string sql,
		Func<bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when()) {
			return result;
		}
		return await builder.InsertAsync(sql, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenInsertIfAsyncCore(
		string sql,
		object parameters,
		Func<bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when()) {
			return result;
		}
		return await builder.InsertAsync(sql, parameters, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenInsertIfAsyncCore<T>(
		string sql,
		Func<T> resultSelector,
		Func<bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		if (!when()) {
			return resultSelector();
		}
		return await builder.InsertAsync(sql, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenInsertIfAsyncCore<T>(
		string sql,
		object parameters,
		Func<T> resultSelector,
		Func<bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		if (!when()) {
			return resultSelector();
		}
		return await builder.InsertAsync(sql, parameters, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	#endregion

	#region Update

	/// <summary>
	/// Chains an UPDATE operation after a successful result.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parameters">The parameters for the UPDATE statement.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenUpdateAsync(
		string sql,
		object? parameters,
		object key,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCore(sql, parameters, key, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an UPDATE operation that returns a value after a successful result.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parameters">The parameters for the UPDATE statement.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="resultSelector">Factory to create the result value after successful update.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenUpdateAsync<T>(
		string sql,
		object? parameters,
		object key,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCore(sql, parameters, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an UPDATE operation after a successful result.
	/// If <paramref name="when"/> returns false, the update is skipped and the chain continues.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parameters">The parameters for the UPDATE statement.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="when">Predicate that determines whether to execute the update.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenUpdateIfAsync(
		string sql,
		object? parameters,
		object key,
		Func<bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateIfAsyncCore(sql, parameters, key, when, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains an UPDATE operation that returns a value after a successful result.
	/// If <paramref name="when"/> returns false, the update is skipped and the chain continues with the result from <paramref name="resultSelector"/>.
	/// </summary>
	/// <param name="sql">The UPDATE SQL statement.</param>
	/// <param name="parameters">The parameters for the UPDATE statement.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="resultSelector">Factory to create the result value after successful update or when skipped.</param>
	/// <param name="when">Predicate that determines whether to execute the update.</param>
	/// <param name="uniqueConstraintMessage">Error message for unique constraint violations.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult<T> ThenUpdateIfAsync<T>(
		string sql,
		object? parameters,
		object key,
		Func<T> resultSelector,
		Func<bool> when,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateIfAsyncCore(sql, parameters, key, resultSelector, when, uniqueConstraintMessage, foreignKeyMessage));

	private async Task<Result> ThenUpdateAsyncCore(
		string sql,
		object? parameters,
		object key,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		return await builder.UpdateAsync(sql, parameters, key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenUpdateAsyncCore<T>(
		string sql,
		object? parameters,
		object key,
		Func<T> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		return await builder.UpdateAsync(sql, parameters, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenUpdateIfAsyncCore(
		string sql,
		object? parameters,
		object key,
		Func<bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when()) {
			return result;
		}
		return await builder.UpdateAsync(sql, parameters, key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<T>> ThenUpdateIfAsyncCore<T>(
		string sql,
		object? parameters,
		object key,
		Func<T> resultSelector,
		Func<bool> when,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}
		if (!when()) {
			return resultSelector();
		}
		return await builder.UpdateAsync(sql, parameters, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	#endregion

	#region Delete

	/// <summary>
	/// Chains a DELETE operation after a successful result.
	/// </summary>
	/// <param name="sql">The DELETE SQL statement.</param>
	/// <param name="parameters">The parameters for the DELETE statement.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenDeleteAsync(
		string sql,
		object? parameters,
		object key,
		string foreignKeyMessage = "Cannot delete, record is in use")
		=> new(builder, this.ThenDeleteAsyncCore(sql, parameters, key, foreignKeyMessage));

	/// <summary>
	/// Conditionally chains a DELETE operation after a successful result.
	/// If <paramref name="when"/> returns false, the delete is skipped and the chain continues.
	/// </summary>
	/// <param name="sql">The DELETE SQL statement.</param>
	/// <param name="parameters">The parameters for the DELETE statement.</param>
	/// <param name="key">The key for not-found error messages.</param>
	/// <param name="when">Predicate that determines whether to execute the delete.</param>
	/// <param name="foreignKeyMessage">Error message for foreign key violations.</param>
	public DbResult ThenDeleteIfAsync(
		string sql,
		object? parameters,
		object key,
		Func<bool> when,
		string foreignKeyMessage = "Cannot delete, record is in use")
		=> new(builder, this.ThenDeleteIfAsyncCore(sql, parameters, key, when, foreignKeyMessage));

	private async Task<Result> ThenDeleteAsyncCore(
		string sql,
		object? parameters,
		object key,
		string foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		return await builder.DeleteAsync(sql, parameters, key, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenDeleteIfAsyncCore(
		string sql,
		object? parameters,
		object key,
		Func<bool> when,
		string foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result;
		}
		if (!when()) {
			return result;
		}
		return await builder.DeleteAsync(sql, parameters, key, foreignKeyMessage).Result.ConfigureAwait(false);
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
	/// Chains a GET operation with mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetAsync<TData, TModel>(string sql, object key, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetAsyncCore(sql, null, key, mapper));

	/// <summary>
	/// Chains a GET operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetAsync<TData, TModel>(string sql, object? parameters, object key, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetAsyncCore(sql, parameters, key, mapper));

	private async Task<Result<TResult>> ThenGetAsyncCore<TResult>(string sql, object? parameters, object key) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetAsync<TResult>(sql, parameters, key).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetAsyncCore<TData, TModel>(string sql, object? parameters, object key, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetAsync(sql, parameters, key, mapper).Result.ConfigureAwait(false);
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
	/// Chains a GET scalar operation with mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetScalarAsync<TData, TModel>(string sql, Func<TData?, TModel> mapper)
		=> new(builder, this.ThenGetScalarAsyncCore(sql, null, mapper));

	/// <summary>
	/// Chains a GET scalar operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetScalarAsync<TData, TModel>(string sql, object? parameters, Func<TData?, TModel> mapper)
		=> new(builder, this.ThenGetScalarAsyncCore(sql, parameters, mapper));

	private async Task<Result<TResult>> ThenGetScalarAsyncCore<TResult>(string sql, object? parameters) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetScalarAsync<TResult>(sql, parameters).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetScalarAsyncCore<TData, TModel>(string sql, object? parameters, Func<TData?, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.GetScalarAsync(sql, parameters, mapper).Result.ConfigureAwait(false);
	}

	#endregion

	#region QueryAny

	/// <summary>
	/// Chains a QueryAny operation after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TResult>> ThenQueryAnyAsync<TResult>(string sql)
		=> new(builder, this.ThenQueryAnyAsyncCore<TResult>(sql, null));

	/// <summary>
	/// Chains a QueryAny operation with parameters after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TResult>> ThenQueryAnyAsync<TResult>(string sql, object? parameters)
		=> new(builder, this.ThenQueryAnyAsyncCore<TResult>(sql, parameters));

	/// <summary>
	/// Chains a QueryAny operation with mapping after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TModel>> ThenQueryAnyAsync<TData, TModel>(string sql, Func<TData, TModel> mapper)
		=> new(builder, this.ThenQueryAnyAsyncCore(sql, null, mapper));

	/// <summary>
	/// Chains a QueryAny operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TModel>> ThenQueryAnyAsync<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper)
		=> new(builder, this.ThenQueryAnyAsyncCore(sql, parameters, mapper));

	private async Task<Result<IReadOnlyList<TResult>>> ThenQueryAnyAsyncCore<TResult>(string sql, object? parameters) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.QueryAnyAsync<TResult>(sql, parameters).Result.ConfigureAwait(false);
	}

	private async Task<Result<IReadOnlyList<TModel>>> ThenQueryAnyAsyncCore<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error;
		}

		return await builder.QueryAnyAsync(sql, parameters, mapper).Result.ConfigureAwait(false);
	}

	#endregion

}