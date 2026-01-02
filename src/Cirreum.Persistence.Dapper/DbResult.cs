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
///     .WhereAsync(d =&gt; d.IsActive, new BadRequestException("Not active"))
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
	/// Chains an INSERT operation that returns a value after a successful result.
	/// </summary>
	public DbResult<T> ThenInsertAsync<T>(
		string sql,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation that returns a value after a successful result.
	/// </summary>
	public DbResult<T> ThenInsertAsync<T>(
		string sql,
		object parameters,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, parameters, resultSelector, uniqueConstraintMessage, foreignKeyMessage));


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
	/// Chains an UPDATE operation that returns a value after a successful result.
	/// </summary>
	public DbResult<T> ThenUpdateAsync<T>(
		string sql,
		object? parameters,
		object key,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCore(sql, parameters, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

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