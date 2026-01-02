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
///     .WhereAsync(d =&gt; d.IsActive, new BadRequestException("Not active"))
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

	#region Where

	/// <summary>
	/// Filters the result based on a predicate.
	/// </summary>
	public DbResult<T> WhereAsync(Func<T, bool> predicate, Exception error)
		=> new(builder, resultTask.WhereAsyncTask(predicate, error));

	/// <summary>
	/// Filters the result based on an async predicate.
	/// </summary>
	public DbResult<T> WhereAsync(Func<T, Task<bool>> predicate, Exception error)
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

	#region Then (for chaining to non-generic Result)

	/// <summary>
	/// Chains to a non-generic Result operation.
	/// </summary>
	public DbResult ThenAsync(Func<T, Task<Result>> next)
		=> new(builder, resultTask.ThenAsyncTask(next));

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
	/// Chains an INSERT operation that returns a value after a successful result.
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
	public DbResult ThenInsertAsync(
		string sql,
		Func<T, object?> parametersFactory,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, parametersFactory, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation that returns a value after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<TResult> ThenInsertAsync<TResult>(
		string sql,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	/// <summary>
	/// Chains an INSERT operation that returns a value after a successful result, using the previous value to build parameters.
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
	public DbResult<TResult> ThenInsertAsync<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenInsertAsyncCore(sql, parametersFactory, resultSelector, uniqueConstraintMessage, foreignKeyMessage));


	private async Task<Result> ThenInsertAsyncCore(
		string sql,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return Cirreum.Result.Fail(result.Error);
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
			return Cirreum.Result.Fail(result.Error);
		}
		return await builder.InsertAsync(sql, parameters, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenInsertAsyncCore(
		string sql,
		Func<T, object?> parametersFactory,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsSuccess) {
			return await builder.InsertAsync(sql, parametersFactory(result.Value), uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		}
		return Cirreum.Result.Fail(result.Error);
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
			return result.Error!;
		}
		return await builder.InsertAsync(sql, parameters, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenInsertAsyncCore<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsSuccess) {
			return await builder.InsertAsync(sql, parametersFactory(result.Value), resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
		}
		return result.Error;
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
	public DbResult ThenUpdateAsync(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCore(sql, parametersFactory, key, uniqueConstraintMessage, foreignKeyMessage));

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
	public DbResult<TResult> ThenUpdateAsync<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(builder, this.ThenUpdateAsyncCore(sql, parametersFactory, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage));

	private async Task<Result> ThenUpdateAsyncCore(
		string sql,
		object? parameters,
		object key,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return Cirreum.Result.Fail(result.Error);
		}
		return await builder.UpdateAsync(sql, parameters, key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenUpdateAsyncCore(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return Cirreum.Result.Fail(result.Error);
		}
		return await builder.UpdateAsync(sql, parametersFactory(result.Value!), key, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
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
			return result.Error!;
		}
		return await builder.UpdateAsync(sql, parameters, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenUpdateAsyncCore<TResult>(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		Func<TResult> resultSelector,
		string uniqueConstraintMessage,
		string? foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}
		return await builder.UpdateAsync(sql, parametersFactory(result.Value!), key, resultSelector, uniqueConstraintMessage, foreignKeyMessage).Result.ConfigureAwait(false);
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
	public DbResult ThenDeleteAsync(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		string foreignKeyMessage = "Cannot delete, record is in use")
		=> new(builder, this.ThenDeleteAsyncCore(sql, parametersFactory, key, foreignKeyMessage));

	private async Task<Result> ThenDeleteAsyncCore(
		string sql,
		object? parameters,
		object key,
		string foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return Cirreum.Result.Fail(result.Error);
		}
		return await builder.DeleteAsync(sql, parameters, key, foreignKeyMessage).Result.ConfigureAwait(false);
	}

	private async Task<Result> ThenDeleteAsyncCore(
		string sql,
		Func<T, object?> parametersFactory,
		object key,
		string foreignKeyMessage) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return Cirreum.Result.Fail(result.Error);
		}
		return await builder.DeleteAsync(sql, parametersFactory(result.Value!), key, foreignKeyMessage).Result.ConfigureAwait(false);
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
		=> new(builder, this.ThenGetAsyncCoreWithMapper<TData, TModel>(sql, null, key, mapper));

	/// <summary>
	/// Chains a GET operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetAsync<TData, TModel>(string sql, object? parameters, object key, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetAsyncCoreWithMapper<TData, TModel>(sql, parameters, key, mapper));

	/// <summary>
	/// Chains a GET operation with mapping after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<TModel> ThenGetAsync<TData, TModel>(string sql, Func<T, object?> parametersFactory, object key, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetAsyncCoreWithMapperAndFactory<TData, TModel>(sql, parametersFactory, key, mapper));

	private async Task<Result<TResult>> ThenGetAsyncCore<TResult>(string sql, object? parameters, object key) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.GetAsync<TResult>(sql, parameters, key).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenGetAsyncCoreWithFactory<TResult>(string sql, Func<T, object?> parametersFactory, object key) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.GetAsync<TResult>(sql, parametersFactory(result.Value!), key).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetAsyncCoreWithMapper<TData, TModel>(string sql, object? parameters, object key, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.GetAsync(sql, parameters, key, mapper).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetAsyncCoreWithMapperAndFactory<TData, TModel>(string sql, Func<T, object?> parametersFactory, object key, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.GetAsync(sql, parametersFactory(result.Value!), key, mapper).Result.ConfigureAwait(false);
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
	public DbResult<TModel> ThenGetScalarAsync<TData, TModel>(string sql, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetScalarAsyncCoreWithMapper<TData, TModel>(sql, null, mapper));

	/// <summary>
	/// Chains a GET scalar operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<TModel> ThenGetScalarAsync<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetScalarAsyncCoreWithMapper<TData, TModel>(sql, parameters, mapper));

	/// <summary>
	/// Chains a GET scalar operation with mapping after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<TModel> ThenGetScalarAsync<TData, TModel>(string sql, Func<T, object?> parametersFactory, Func<TData, TModel> mapper)
		=> new(builder, this.ThenGetScalarAsyncCoreWithMapperAndFactory<TData, TModel>(sql, parametersFactory, mapper));

	private async Task<Result<TResult>> ThenGetScalarAsyncCore<TResult>(string sql, object? parameters) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.GetScalarAsync<TResult>(sql, parameters).Result.ConfigureAwait(false);
	}

	private async Task<Result<TResult>> ThenGetScalarAsyncCoreWithFactory<TResult>(string sql, Func<T, object?> parametersFactory) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.GetScalarAsync<TResult>(sql, parametersFactory(result.Value!)).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetScalarAsyncCoreWithMapper<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.GetScalarAsync(sql, parameters, mapper).Result.ConfigureAwait(false);
	}

	private async Task<Result<TModel>> ThenGetScalarAsyncCoreWithMapperAndFactory<TData, TModel>(string sql, Func<T, object?> parametersFactory, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.GetScalarAsync(sql, parametersFactory(result.Value!), mapper).Result.ConfigureAwait(false);
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
		=> new(builder, this.ThenQueryAnyAsyncCoreWithMapper<TData, TModel>(sql, null, mapper));

	/// <summary>
	/// Chains a QueryAny operation with parameters and mapping after a successful result.
	/// </summary>
	public DbResult<IReadOnlyList<TModel>> ThenQueryAnyAsync<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper)
		=> new(builder, this.ThenQueryAnyAsyncCoreWithMapper<TData, TModel>(sql, parameters, mapper));

	/// <summary>
	/// Chains a QueryAny operation with mapping after a successful result, using the previous value to build parameters.
	/// </summary>
	public DbResult<IReadOnlyList<TModel>> ThenQueryAnyAsync<TData, TModel>(string sql, Func<T, object?> parametersFactory, Func<TData, TModel> mapper)
		=> new(builder, this.ThenQueryAnyAsyncCoreWithMapperAndFactory<TData, TModel>(sql, parametersFactory, mapper));

	private async Task<Result<IReadOnlyList<TResult>>> ThenQueryAnyAsyncCore<TResult>(string sql, object? parameters) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.QueryAnyAsync<TResult>(sql, parameters).Result.ConfigureAwait(false);
	}

	private async Task<Result<IReadOnlyList<TResult>>> ThenQueryAnyAsyncCoreWithFactory<TResult>(string sql, Func<T, object?> parametersFactory) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.QueryAnyAsync<TResult>(sql, parametersFactory(result.Value!)).Result.ConfigureAwait(false);
	}

	private async Task<Result<IReadOnlyList<TModel>>> ThenQueryAnyAsyncCoreWithMapper<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.QueryAnyAsync(sql, parameters, mapper).Result.ConfigureAwait(false);
	}

	private async Task<Result<IReadOnlyList<TModel>>> ThenQueryAnyAsyncCoreWithMapperAndFactory<TData, TModel>(string sql, Func<T, object?> parametersFactory, Func<TData, TModel> mapper) {
		var result = await resultTask.ConfigureAwait(false);
		if (result.IsFailure) {
			return result.Error!;
		}

		return await builder.QueryAnyAsync(sql, parametersFactory(result.Value!), mapper).Result.ConfigureAwait(false);
	}

	#endregion

}