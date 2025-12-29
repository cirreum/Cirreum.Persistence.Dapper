namespace Cirreum.Persistence;

using Cirreum;
using Cirreum.Exceptions;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

/// <summary>
/// Provides extension methods for executing SQL queries using Dapper and returning results wrapped in Result types,
/// including support for single, multiple, and paginated query results with optional mapping functions.
/// </summary>
/// <remarks>These extension methods simplify common query patterns by integrating Dapper query execution with the
/// Result and PagedResult types. They support asynchronous operations, parameterized queries, and mapping between data
/// and domain models. Methods are designed to handle not-found cases and pagination scenarios, and to promote
/// consistent result handling across data access layers.</remarks>
public static class DapperQueryExtensions {

	extension(IDbConnection conn) {

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns a single result wrapped in a <see cref="Result{T}"/>
		/// object.
		/// </summary>
		/// <typeparam name="T">The type of the object to be returned from the query.</typeparam>
		/// <param name="sql">The SQL query to execute. Should be a statement that returns a single row.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are
		/// required.</param>
		/// <param name="key">A key associated with the query result, used to identify or correlate the returned value.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation. The result contains a <see cref="Result{T}"/> object with the
		/// queried value, or a NotFound result if no row is found.</returns>
		public async Task<Result<T>> QuerySingleAsync<T>(
			string sql,
			object? param,
			object key,
			CancellationToken cancellationToken = default) {
			var result = await conn.QuerySingleOrDefaultAsync<T>(new CommandDefinition(
				sql,
				param,
				cancellationToken: cancellationToken));
			return Result.FromLookup(result, key);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns a single result wrapped in a <see cref="Result{T}"/>
		/// object, applying a mapping function to transform the item.
		/// </summary>
		/// <typeparam name="TData">The type of the object returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the object in the final result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute. Should be a statement that returns a single row.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="key">A key associated with the query result, used to identify or correlate the returned value.</param>
		/// <param name="mapper">A function to transform the data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation. The result contains a <see cref="Result{T}"/> object with the
		/// mapped value, or a NotFound result if no row is found.</returns>
		public async Task<Result<TModel>> QuerySingleAsync<TData, TModel>(
			string sql,
			object? param,
			object key,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			var result = await conn.QuerySingleOrDefaultAsync<TData>(new CommandDefinition(
				sql,
				param,
				cancellationToken: cancellationToken));
			if (result is null) {
				return Result.NotFound<TModel>(key);
			}
			return mapper(result);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns zero or more results as a read-only list
		/// wrapped in a successful Result.
		/// </summary>
		/// <typeparam name="T">The type of the elements to be returned in the result list.</typeparam>
		/// <param name="sql">The SQL query to execute against the database.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or null if no parameters are required.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful Result
		/// object wrapping a read-only list of items (which may be empty).</returns>
		public async Task<Result<IReadOnlyList<T>>> QueryAnyAsync<T>(
			string sql,
			object? param,
			CancellationToken cancellationToken = default) {
			var result = await conn.QueryAsync<T>(new CommandDefinition(
				sql,
				param,
				cancellationToken: cancellationToken));
			return Result.From<IReadOnlyList<T>>([.. result]);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns zero or more results as a read-only list
		/// wrapped in a successful Result, applying a mapping function to transform each item.
		/// </summary>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final result list (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute against the database.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or null if no parameters are required.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful Result
		/// object wrapping a read-only list of mapped items (which may be empty).</returns>
		public async Task<Result<IReadOnlyList<TModel>>> QueryAnyAsync<TData, TModel>(
			string sql,
			object? param,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			var result = await conn.QueryAsync<TData>(new CommandDefinition(
				sql,
				param,
				cancellationToken: cancellationToken));
			return Result.From<IReadOnlyList<TModel>>([.. result.Select(mapper)]);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the results as a paginated result wrapped in a
		/// <see cref="Result{T}"/> object.
		/// </summary>
		/// <remarks>
		/// This method expects an SQL query that includes OFFSET/FETCH clauses for pagination. The total count must be
		/// obtained separately before calling this method, typically via a COUNT(*) query.
		/// </remarks>
		/// <typeparam name="T">The type of the elements to be returned in the paged result.</typeparam>
		/// <param name="sql">The SQL query to execute against the database. Should include OFFSET/FETCH for pagination.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="totalCount">The total number of records matching the query criteria (before pagination).</param>
		/// <param name="pageSize">The number of items per page.</param>
		/// <param name="page">The current page number (1-based).</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="PagedResult{T}"/> with the queried items and pagination metadata.</returns>
		public async Task<Result<PagedResult<T>>> QueryPagedAsync<T>(
			string sql,
			object? param,
			int totalCount,
			int pageSize,
			int page,
			CancellationToken cancellationToken = default) {
			var items = await conn.QueryAsync<T>(new CommandDefinition(
				sql,
				param,
				cancellationToken: cancellationToken));
			return new PagedResult<T>(
				[.. items],
				totalCount,
				pageSize,
				page
			);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the results as a paginated result wrapped in a
		/// <see cref="Result{T}"/> object, applying a mapping function to transform each item.
		/// </summary>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final paged result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute against the database. Should include OFFSET/FETCH for pagination.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="totalCount">The total number of records matching the query criteria (before pagination).</param>
		/// <param name="pageSize">The number of items per page.</param>
		/// <param name="page">The current page number (1-based).</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="PagedResult{T}"/> with the mapped items and pagination metadata.</returns>
		public async Task<Result<PagedResult<TModel>>> QueryPagedAsync<TData, TModel>(
			string sql,
			object? param,
			int totalCount,
			int pageSize,
			int page,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			var items = await conn.QueryAsync<TData>(new CommandDefinition(
				sql,
				param,
				cancellationToken: cancellationToken));
			return new PagedResult<TModel>(
				[.. items.Select(mapper)],
				totalCount,
				pageSize,
				page
			);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the results as a cursor-based paginated result
		/// wrapped in a <see cref="Result{T}"/> object.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method automatically injects a <c>@PageSize</c> parameter set to <paramref name="pageSize"/> + 1 to
		/// determine if additional pages exist. Your SQL query should use <c>TOP (@PageSize)</c>.
		/// </para>
		/// <para>
		/// The query should include a WHERE clause for cursor positioning when a cursor is provided. Use
		/// <see cref="Cursor.Decode{TColumn}"/> to decode the cursor and pass <c>cursor?.Column</c> and
		/// <c>cursor?.Id</c> as parameters.
		/// </para>
		/// </remarks>
		/// <typeparam name="T">The type of the elements to be returned in the cursor result.</typeparam>
		/// <typeparam name="TColumn">The type of the sort column used for cursor positioning.</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="pageSize">The maximum number of items to return per page.</param>
		/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier from an item for cursor encoding.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="CursorResult{T}"/> with the queried items and cursor metadata.</returns>
		public async Task<Result<CursorResult<T>>> QueryCursorAsync<T, TColumn>(
			string sql,
			object? param,
			int pageSize,
			Func<T, (TColumn Column, Guid Id)> cursorSelector,
			CancellationToken cancellationToken = default) {

			var parameters = new DynamicParameters(param);
			parameters.Add("PageSize", pageSize + 1);

			var items = (await conn.QueryAsync<T>(new CommandDefinition(
				sql,
				parameters,
				cancellationToken: cancellationToken))).ToList();

			var hasNextPage = items.Count > pageSize;
			if (hasNextPage) {
				items.RemoveAt(items.Count - 1);
			}

			string? nextCursor = null;
			if (hasNextPage && items.Count > 0) {
				var (column, id) = cursorSelector(items[^1]);
				nextCursor = Cursor.Encode(column, id);
			}

			return new CursorResult<T>(items, nextCursor, hasNextPage);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the results as a cursor-based paginated result
		/// wrapped in a <see cref="Result{T}"/> object, applying a mapping function to transform each item.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method automatically injects a <c>@PageSize</c> parameter set to <paramref name="pageSize"/> + 1 to
		/// determine if additional pages exist. Your SQL query should use <c>TOP (@PageSize)</c>.
		/// </para>
		/// <para>
		/// The query should include a WHERE clause for cursor positioning when a cursor is provided. Use
		/// <see cref="Cursor.Decode{TColumn}"/> to decode the cursor and pass <c>cursor?.Column</c> and
		/// <c>cursor?.Id</c> as parameters.
		/// </para>
		/// </remarks>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final cursor result (domain layer).</typeparam>
		/// <typeparam name="TColumn">The type of the sort column used for cursor positioning.</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="pageSize">The maximum number of items to return per page.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier from a mapped item for cursor encoding.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="CursorResult{T}"/> with the mapped items and cursor metadata.</returns>
		public async Task<Result<CursorResult<TModel>>> QueryCursorAsync<TData, TModel, TColumn>(
			string sql,
			object? param,
			int pageSize,
			Func<TData, TModel> mapper,
			Func<TModel, (TColumn Column, Guid Id)> cursorSelector,
			CancellationToken cancellationToken = default) {

			var parameters = new DynamicParameters(param);
			parameters.Add("PageSize", pageSize + 1);

			var data = (await conn.QueryAsync<TData>(new CommandDefinition(
				sql,
				parameters,
				cancellationToken: cancellationToken))).ToList();

			var hasNextPage = data.Count > pageSize;
			if (hasNextPage) {
				data.RemoveAt(data.Count - 1);
			}

			var items = data.Select(mapper).ToList();

			string? nextCursor = null;
			if (hasNextPage && items.Count > 0) {
				var (column, id) = cursorSelector(items[^1]);
				nextCursor = Cursor.Encode(column, id);
			}

			return new CursorResult<TModel>(items, nextCursor, hasNextPage);

		}


		/// <summary>
		/// Executes the specified SQL command asynchronously and returns a result indicating success or failure, with
		/// specialized handling for unique constraint and foreign key violations.
		/// </summary>
		/// <remarks>
		/// If the SQL command fails due to a unique constraint or foreign key violation, the returned Result
		/// will indicate failure and include an appropriate exception. For all other SQL errors, the exception
		/// is not handled and will propagate to the caller.
		/// </remarks>
		/// <param name="sql">The SQL command to execute. This should be a valid statement supported by the underlying database.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL command, or null if no parameters are required.</param>
		/// <param name="uniqueConstraintMessage">An optional custom error message to use if a unique constraint violation occurs. If null, a default message is used.</param>
		/// <param name="foreignKeyMessage">An optional custom error message to use if a foreign key violation occurs. If null, a default message is used.</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
		/// <returns>
		/// A task that represents the asynchronous operation. The task result contains a <see cref="Result"/> 
		/// indicating whether the command executed successfully or failed due to a unique constraint or foreign key
		/// violation.
		/// </returns>
		public async Task<Result> ExecuteCommandAsync(
			string sql,
			object? param,
			string? uniqueConstraintMessage = null,
			string? foreignKeyMessage = null,
			CancellationToken cancellationToken = default) {

			try {
				await conn.ExecuteAsync(new CommandDefinition(
					sql,
					param,
					cancellationToken: cancellationToken));

				return Result.Success;
			} catch (SqlException ex) when (ex.IsUniqueConstraintViolation()) {
				// TODO: once the non-generic Result.Fail overload is removed, change to Result.AlreadyExist
				return Result.Fail(new AlreadyExistsException(uniqueConstraintMessage ?? "Record already exists"));
			} catch (SqlException ex) when (ex.IsForeignKeyViolation()) {
				// TODO: once the non-generic Result.Fail overload is removed, change to Result.BadRequest
				return Result.Fail(new BadRequestException(foreignKeyMessage ?? "Referenced record not found"));
			}
		}

		/// <summary>
		/// Executes a SQL command asynchronously and returns the result using the specified selector function. Handles unique
		/// constraint and foreign key violations by returning appropriate error results.
		/// </summary>
		/// <remarks>If a unique constraint or foreign key violation is detected, the method returns a failed result
		/// with the provided custom message or a default message. The selector function is only invoked if the command
		/// executes without constraint violations.</remarks>
		/// <typeparam name="T">The type of the result returned by the selector function.</typeparam>
		/// <param name="sql">The SQL command to execute against the database.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL command, or null if no parameters are required.</param>
		/// <param name="selector">A function that creates the result of type T after the command executes successfully.</param>
		/// <param name="uniqueConstraintMessage">An optional custom error message to use if a unique constraint violation occurs. If null, a default message is
		/// used.</param>
		/// <param name="foreignKeyMessage">An optional custom error message to use if a foreign key violation occurs. If null, a default message is used.</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
		/// <returns>
		/// A <see cref="Result{T}"/> representing the outcome of the command. Returns a successful result if the command executes and the
		/// selector completes successfully; returns an error result if a unique constraint or foreign key violation occurs.
		/// </returns>
		public async Task<Result<T>> ExecuteCommandAsync<T>(
			string sql,
			object? param,
			Func<T> selector,
			string? uniqueConstraintMessage = null,
			string? foreignKeyMessage = null,
			CancellationToken cancellationToken = default) {

			try {
				await conn.ExecuteAsync(new CommandDefinition(
					sql,
					param,
					cancellationToken: cancellationToken));

				return selector();
			} catch (SqlException ex) when (ex.IsUniqueConstraintViolation()) {
				return Result.AlreadyExist<T>(uniqueConstraintMessage ?? "Record already exists");
			} catch (SqlException ex) when (ex.IsForeignKeyViolation()) {
				return Result.BadRequest<T>(foreignKeyMessage ?? "Referenced record not found");
			}
		}

	}

}