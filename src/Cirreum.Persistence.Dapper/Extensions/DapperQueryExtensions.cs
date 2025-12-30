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
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// -- First page (no cursor)
		/// SELECT TOP (@PageSize) *
		/// FROM Orders
		/// WHERE CustomerId = @CustomerId
		/// ORDER BY CreatedAt DESC, OrderId DESC
		///
		/// -- Subsequent pages (with cursor)
		/// SELECT TOP (@PageSize) *
		/// FROM Orders
		/// WHERE CustomerId = @CustomerId
		///   AND (CreatedAt &lt; @Column 
		///        OR (CreatedAt = @Column AND OrderId &lt; @Id))
		/// ORDER BY CreatedAt DESC, OrderId DESC
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// var cursor = Cursor.Decode&lt;DateTime&gt;(query.Cursor);
		///
		/// var sql = cursor is null
		///     ? "SELECT TOP (@PageSize) ... ORDER BY CreatedAt DESC, Id DESC"
		///     : "SELECT TOP (@PageSize) ... WHERE (CreatedAt &lt; @Column OR (CreatedAt = @Column AND Id &lt; @Id)) ORDER BY CreatedAt DESC, Id DESC";
		///
		/// return await conn.QueryCursorAsync&lt;Order, DateTime&gt;(
		///     sql,
		///     new { query.CustomerId, cursor?.Column, cursor?.Id },
		///     query.PageSize,
		///     o =&gt; (o.CreatedAt, o.Id),
		///     cancellationToken);
		/// </code>
		/// <para>
		/// The returned cursor is URL-safe base64 encoded and can be passed directly in query strings.
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
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// -- First page (no cursor)
		/// SELECT TOP (@PageSize) *
		/// FROM Orders
		/// WHERE CustomerId = @CustomerId
		/// ORDER BY CreatedAt DESC, OrderId DESC
		///
		/// -- Subsequent pages (with cursor)
		/// SELECT TOP (@PageSize) *
		/// FROM Orders
		/// WHERE CustomerId = @CustomerId
		///   AND (CreatedAt &lt; @Column 
		///        OR (CreatedAt = @Column AND OrderId &lt; @Id))
		/// ORDER BY CreatedAt DESC, OrderId DESC
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// var cursor = Cursor.Decode&lt;DateTime&gt;(query.Cursor);
		///
		/// var sql = cursor is null
		///     ? "SELECT TOP (@PageSize) ... ORDER BY CreatedAt DESC, Id DESC"
		///     : "SELECT TOP (@PageSize) ... WHERE (CreatedAt &lt; @Column OR (CreatedAt = @Column AND Id &lt; @Id)) ORDER BY CreatedAt DESC, Id DESC";
		///
		/// return await conn.QueryCursorAsync&lt;OrderData, Order, DateTime&gt;(
		///     sql,
		///     new { query.CustomerId, cursor?.Column, cursor?.Id },
		///     query.PageSize,
		///     data =&gt; new Order(data),
		///     o =&gt; (o.CreatedAt, o.Id),
		///     cancellationToken);
		/// </code>
		/// <para>
		/// The returned cursor is URL-safe base64 encoded and can be passed directly in query strings.
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
		/// Executes the specified SQL query asynchronously and returns a slice of results with an indicator
		/// for whether more items exist.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method automatically injects a <c>@PageSize</c> parameter set to <paramref name="pageSize"/> + 1 to
		/// determine if additional items exist. Your SQL query should use <c>TOP (@PageSize)</c>.
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// SELECT TOP (@PageSize) *
		/// FROM Orders
		/// WHERE CustomerId = @CustomerId
		/// ORDER BY CreatedAt DESC
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await conn.QuerySliceAsync&lt;Order&gt;(
		///     "SELECT TOP (@PageSize) ... ORDER BY CreatedAt DESC",
		///     new { query.CustomerId },
		///     query.PageSize,
		///     cancellationToken);
		/// </code>
		/// <para>
		/// Use this for simple "load more" patterns without full pagination metadata.
		/// For stable cursor-based pagination, use <see cref="QueryCursorAsync{T, TColumn}"/> instead.
		/// For full pagination with total counts, use <see cref="QueryPagedAsync{T}"/> instead.
		/// </para>
		/// </remarks>
		/// <typeparam name="T">The type of the elements to be returned in the slice result.</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="pageSize">The maximum number of items to return.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="SliceResult{T}"/> with the queried items and a flag indicating if more items exist.</returns>
		public async Task<Result<SliceResult<T>>> QuerySliceAsync<T>(
			string sql,
			object? param,
			int pageSize,
			CancellationToken cancellationToken = default) {

			var parameters = new DynamicParameters(param);
			parameters.Add("PageSize", pageSize + 1);

			var items = (await conn.QueryAsync<T>(new CommandDefinition(
				sql,
				parameters,
				cancellationToken: cancellationToken))).ToList();

			var hasMore = items.Count > pageSize;
			if (hasMore) {
				items.RemoveAt(items.Count - 1);
			}

			return new SliceResult<T>(items, hasMore);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns a slice of results with an indicator
		/// for whether more items exist, applying a mapping function to transform each item.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method automatically injects a <c>@PageSize</c> parameter set to <paramref name="pageSize"/> + 1 to
		/// determine if additional items exist. Your SQL query should use <c>TOP (@PageSize)</c>.
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// SELECT TOP (@PageSize) *
		/// FROM Orders
		/// WHERE CustomerId = @CustomerId
		/// ORDER BY CreatedAt DESC
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await conn.QuerySliceAsync&lt;OrderData, Order&gt;(
		///     "SELECT TOP (@PageSize) ... ORDER BY CreatedAt DESC",
		///     new { query.CustomerId },
		///     query.PageSize,
		///     data =&gt; new Order(data),
		///     cancellationToken);
		/// </code>
		/// <para>
		/// Use this for simple "load more" patterns without full pagination metadata.
		/// For stable cursor-based pagination, use <see cref="QueryCursorAsync{TData, TModel, TColumn}"/> instead.
		/// For full pagination with total counts, use <see cref="QueryPagedAsync{TData, TModel}"/> instead.
		/// </para>
		/// </remarks>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final slice result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="pageSize">The maximum number of items to return.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="SliceResult{T}"/> with the mapped items and a flag indicating if more items exist.</returns>
		public async Task<Result<SliceResult<TModel>>> QuerySliceAsync<TData, TModel>(
			string sql,
			object? param,
			int pageSize,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {

			var parameters = new DynamicParameters(param);
			parameters.Add("PageSize", pageSize + 1);

			var data = (await conn.QueryAsync<TData>(new CommandDefinition(
				sql,
				parameters,
				cancellationToken: cancellationToken))).ToList();

			var hasMore = data.Count > pageSize;
			if (hasMore) {
				data.RemoveAt(data.Count - 1);
			}

			var items = data.Select(mapper).ToList();

			return new SliceResult<TModel>(items, hasMore);
		}


		/// <summary>
		/// Executes an INSERT command and returns a successful result.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Use this method for INSERT operations where constraint violations should be converted to Result failures.
		/// Unique constraint violations become <see cref="AlreadyExistsException"/> (HTTP 409).
		/// Foreign key violations become <see cref="BadRequestException"/> (HTTP 400, referenced record doesn't exist).
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// INSERT INTO Orders (OrderId, CustomerId, Amount, CreatedAt)
		/// VALUES (@OrderId, @CustomerId, @Amount, @CreatedAt)
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await conn.InsertAsync(
		///     "INSERT INTO Orders (OrderId, CustomerId, Amount) VALUES (@OrderId, @CustomerId, @Amount)",
		///     new { OrderId = Guid.CreateVersion7(), command.CustomerId, command.Amount },
		///     uniqueConstraintMessage: "Order already exists",
		///     foreignKeyMessage: "Customer not found",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <param name="sql">The SQL INSERT statement to execute.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs, or <see langword="null"/> to let the exception propagate.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful <see cref="Result"/>
		/// or a failure result with an appropriate exception.</returns>
		public async Task<Result> InsertAsync(
			string sql,
			object? param,
			string uniqueConstraintMessage = "Record already exists",
			string? foreignKeyMessage = "Referenced record does not exist",
			CancellationToken cancellationToken = default) {

			try {
				await conn.ExecuteAsync(new CommandDefinition(
					sql,
					param,
					cancellationToken: cancellationToken));

				return Result.Success;
			} catch (SqlException ex) when (ex.IsUniqueConstraintViolation()) {
				return Result.Fail(new AlreadyExistsException(uniqueConstraintMessage));
			} catch (SqlException ex) when (foreignKeyMessage is not null && ex.IsForeignKeyViolation()) {
				return Result.Fail(new BadRequestException(foreignKeyMessage));
			}
		}

		/// <summary>
		/// Executes an INSERT command and returns the specified value on success.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Use this method for INSERT operations that return a client-generated value (e.g., a Guid created before insert).
		/// Unique constraint violations become <see cref="AlreadyExistsException"/> (HTTP 409).
		/// Foreign key violations become <see cref="BadRequestException"/> (HTTP 400, referenced record doesn't exist).
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// INSERT INTO Orders (OrderId, CustomerId, Amount, CreatedAt)
		/// VALUES (@OrderId, @CustomerId, @Amount, @CreatedAt)
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// var orderId = Guid.CreateVersion7();
		///
		/// return await conn.InsertAsync(
		///     "INSERT INTO Orders (OrderId, CustomerId, Amount) VALUES (@OrderId, @CustomerId, @Amount)",
		///     new { OrderId = orderId, command.CustomerId, command.Amount },
		///     () =&gt; orderId,
		///     uniqueConstraintMessage: "Order already exists",
		///     foreignKeyMessage: "Customer not found",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <typeparam name="T">The type of the value to return on success.</typeparam>
		/// <param name="sql">The SQL INSERT statement to execute.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="resultSelector">A function that returns the value to include in the successful result.</param>
		/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs, or <see langword="null"/> to let the exception propagate.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// with the value from <paramref name="resultSelector"/> on success, or a failure result with an appropriate exception.</returns>
		public async Task<Result<T>> InsertAsync<T>(
			string sql,
			object? param,
			Func<T> resultSelector,
			string uniqueConstraintMessage = "Record already exists",
			string? foreignKeyMessage = "Referenced record does not exist",
			CancellationToken cancellationToken = default) {

			try {
				await conn.ExecuteAsync(new CommandDefinition(
					sql,
					param,
					cancellationToken: cancellationToken));

				return resultSelector();
			} catch (SqlException ex) when (ex.IsUniqueConstraintViolation()) {
				return Result.AlreadyExist<T>(uniqueConstraintMessage);
			} catch (SqlException ex) when (foreignKeyMessage is not null && ex.IsForeignKeyViolation()) {
				return Result.BadRequest<T>(foreignKeyMessage);
			}
		}

		/// <summary>
		/// Executes an UPDATE command and returns a successful result if at least one row was affected.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Use this method for UPDATE operations where no rows affected indicates the record was not found.
		/// Returns <see cref="NotFoundException"/> (HTTP 404) if no rows were updated.
		/// Unique constraint violations become <see cref="AlreadyExistsException"/> (HTTP 409).
		/// Foreign key violations become <see cref="BadRequestException"/> (HTTP 400, referenced record doesn't exist).
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// UPDATE Orders
		/// SET Amount = @Amount, UpdatedAt = @UpdatedAt
		/// WHERE OrderId = @OrderId
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await conn.UpdateAsync(
		///     "UPDATE Orders SET Amount = @Amount WHERE OrderId = @OrderId",
		///     new { command.OrderId, command.Amount },
		///     key: command.OrderId,
		///     uniqueConstraintMessage: "Order with this reference already exists",
		///     foreignKeyMessage: "Customer not found",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <param name="sql">The SQL UPDATE statement to execute.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="key">The key of the entity being updated, used in the <see cref="NotFoundException"/> if no rows are affected.</param>
		/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs, or <see langword="null"/> to let the exception propagate.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful <see cref="Result"/>
		/// if at least one row was updated, or a failure result with an appropriate exception.</returns>
		public async Task<Result> UpdateAsync(
			string sql,
			object? param,
			object key,
			string uniqueConstraintMessage = "Record already exists",
			string? foreignKeyMessage = "Referenced record does not exist",
			CancellationToken cancellationToken = default) {

			try {
				var rowsAffected = await conn.ExecuteAsync(new CommandDefinition(
					sql,
					param,
					cancellationToken: cancellationToken));

				return rowsAffected > 0
					? Result.Success
					: Result.Fail(new NotFoundException(key));
			} catch (SqlException ex) when (ex.IsUniqueConstraintViolation()) {
				return Result.Fail(new AlreadyExistsException(uniqueConstraintMessage));
			} catch (SqlException ex) when (foreignKeyMessage is not null && ex.IsForeignKeyViolation()) {
				return Result.Fail(new BadRequestException(foreignKeyMessage));
			}
		}

		/// <summary>
		/// Executes an UPDATE command and returns the specified value if at least one row was affected.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Use this method for UPDATE operations that return a value on success.
		/// Returns <see cref="NotFoundException"/> (HTTP 404) if no rows were updated.
		/// Unique constraint violations become <see cref="AlreadyExistsException"/> (HTTP 409).
		/// Foreign key violations become <see cref="BadRequestException"/> (HTTP 400, referenced record doesn't exist).
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// UPDATE Orders
		/// SET Amount = @Amount, UpdatedAt = @UpdatedAt
		/// WHERE OrderId = @OrderId
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await conn.UpdateAsync(
		///     "UPDATE Orders SET Amount = @Amount WHERE OrderId = @OrderId",
		///     new { command.OrderId, command.Amount },
		///     key: command.OrderId,
		///     () =&gt; command.OrderId,
		///     uniqueConstraintMessage: "Order with this reference already exists",
		///     foreignKeyMessage: "Customer not found",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <typeparam name="T">The type of the value to return on success.</typeparam>
		/// <param name="sql">The SQL UPDATE statement to execute.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="key">The key of the entity being updated, used in the <see cref="NotFoundException"/> if no rows are affected.</param>
		/// <param name="resultSelector">A function that returns the value to include in the successful result.</param>
		/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs, or <see langword="null"/> to let the exception propagate.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// with the value from <paramref name="resultSelector"/> if at least one row was updated, or a failure result with an appropriate exception.</returns>
		public async Task<Result<T>> UpdateAsync<T>(
			string sql,
			object? param,
			object key,
			Func<T> resultSelector,
			string uniqueConstraintMessage = "Record already exists",
			string? foreignKeyMessage = "Referenced record does not exist",
			CancellationToken cancellationToken = default) {

			try {
				var rowsAffected = await conn.ExecuteAsync(new CommandDefinition(
					sql,
					param,
					cancellationToken: cancellationToken));

				return rowsAffected > 0
					? resultSelector()
					: Result.NotFound<T>(key);
			} catch (SqlException ex) when (ex.IsUniqueConstraintViolation()) {
				return Result.AlreadyExist<T>(uniqueConstraintMessage);
			} catch (SqlException ex) when (foreignKeyMessage is not null && ex.IsForeignKeyViolation()) {
				return Result.BadRequest<T>(foreignKeyMessage);
			}
		}

		/// <summary>
		/// Executes a DELETE command and returns a successful result if at least one row was affected.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Use this method for DELETE operations where no rows affected indicates the record was not found.
		/// Returns <see cref="NotFoundException"/> (HTTP 404) if no rows were deleted.
		/// Foreign key violations become <see cref="ConflictException"/> (HTTP 409, record is still referenced by other records).
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// DELETE FROM Orders
		/// WHERE OrderId = @OrderId
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await conn.DeleteAsync(
		///     "DELETE FROM Orders WHERE OrderId = @OrderId",
		///     new { command.OrderId },
		///     key: command.OrderId,
		///     foreignKeyMessage: "Cannot delete order, it has associated line items",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <param name="sql">The SQL DELETE statement to execute.</param>
		/// <param name="param">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="key">The key of the entity being deleted, used in the <see cref="NotFoundException"/> if no rows are affected.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful <see cref="Result"/>
		/// if at least one row was deleted, or a failure result with an appropriate exception.</returns>
		public async Task<Result> DeleteAsync(
			string sql,
			object? param,
			object key,
			string foreignKeyMessage = "Cannot delete, record is in use",
			CancellationToken cancellationToken = default) {

			try {
				var rowsAffected = await conn.ExecuteAsync(new CommandDefinition(
					sql,
					param,
					cancellationToken: cancellationToken));

				return rowsAffected > 0
					? Result.Success
					: Result.Fail(new NotFoundException(key));
			} catch (SqlException ex) when (ex.IsForeignKeyViolation()) {
				return Result.Fail(new ConflictException(foreignKeyMessage));
			}
		}

	}

}