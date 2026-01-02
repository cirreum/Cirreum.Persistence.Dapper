namespace Cirreum.Persistence;

using Cirreum;
using System.Data;

/// <summary>
/// Provides a fluent interface for executing database operations within a transaction,
/// automatically flowing the connection, transaction, and cancellation token to each operation.
/// </summary>
/// <remarks>
/// <para>
/// Use this context within <see cref="DbConnectionExtensions.ExecuteTransactionAsync"/> or
/// <see cref="DbConnectionFactoryExtensions.ExecuteTransactionAsync"/> to chain multiple
/// database operations that should be executed atomically.
/// </para>
/// <para>
/// <strong>Usage with IDbConnection:</strong>
/// </para>
/// <code>
/// await using var conn = await db.CreateConnectionAsync(cancellationToken);
/// return await conn.ExecuteTransactionAsync(ctx => ctx
///     .InsertAsync(orderSql, orderParam)
///     .ThenInsertAsync(lineItemSql, lineItemParam)
///     .ThenUpdateAsync(inventorySql, inventoryParam, inventoryId)
/// , cancellationToken);
/// </code>
/// <para>
/// <strong>Usage with IDbConnectionFactory:</strong>
/// </para>
/// <code>
/// return await db.ExecuteTransactionAsync(ctx => ctx
///     .InsertAsync(orderSql, orderParam)
///     .ThenInsertAsync(lineItemSql, lineItemParam)
///     .ThenUpdateAsync(inventorySql, inventoryParam, inventoryId)
/// , cancellationToken);
/// </code>
/// </remarks>
public readonly struct TransactionContext(
	IDbConnection connection,
	IDbTransaction transaction,
	CancellationToken cancellationToken) {

	#region Insert

	/// <summary>
	/// Executes an INSERT command and returns a chainable result.
	/// </summary>
	/// <param name="sql">The SQL INSERT statement to execute.</param>
	/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
	/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
	/// <returns>A <see cref="DbResult"/> that can be chained with other database operations.</returns>
	public DbResult InsertAsync(
		string sql,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(this, connection.InsertAsync(sql, null, uniqueConstraintMessage, foreignKeyMessage, transaction, cancellationToken));

	/// <summary>
	/// Executes an INSERT command and returns a chainable result.
	/// </summary>
	/// <param name="sql">The SQL INSERT statement to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL command.</param>
	/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
	/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
	/// <returns>A <see cref="DbResult"/> that can be chained with other database operations.</returns>
	public DbResult InsertAsync(
		string sql,
		object? parameters,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(this, connection.InsertAsync(sql, parameters, uniqueConstraintMessage, foreignKeyMessage, transaction, cancellationToken));

	/// <summary>
	/// Executes an INSERT command and returns the specified value on success.
	/// </summary>
	/// <typeparam name="T">The type of the value to return on success.</typeparam>
	/// <param name="sql">The SQL INSERT statement to execute.</param>
	/// <param name="resultSelector">A function that returns the value to include in the successful result.</param>
	/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
	/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<T> InsertAndReturnAsync<T>(
		string sql,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(this, connection.InsertAsync(sql, null, resultSelector, uniqueConstraintMessage, foreignKeyMessage, transaction, cancellationToken));

	/// <summary>
	/// Executes an INSERT command and returns the specified value on success.
	/// </summary>
	/// <typeparam name="T">The type of the value to return on success.</typeparam>
	/// <param name="sql">The SQL INSERT statement to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL command.</param>
	/// <param name="resultSelector">A function that returns the value to include in the successful result.</param>
	/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
	/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<T> InsertAndReturnAsync<T>(
		string sql,
		object? parameters,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(this, connection.InsertAsync(sql, parameters, resultSelector, uniqueConstraintMessage, foreignKeyMessage, transaction, cancellationToken));

	#endregion

	#region Update

	/// <summary>
	/// Executes an UPDATE command and returns a chainable result if at least one row was affected.
	/// </summary>
	/// <param name="sql">The SQL UPDATE statement to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL command.</param>
	/// <param name="key">The key of the entity being updated.</param>
	/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
	/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
	/// <returns>A <see cref="DbResult"/> that can be chained with other database operations.</returns>
	public DbResult UpdateAsync(
		string sql,
		object? parameters,
		object key,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(this, connection.UpdateAsync(sql, parameters, key, uniqueConstraintMessage, foreignKeyMessage, transaction, cancellationToken));

	/// <summary>
	/// Executes an UPDATE command and returns the specified value if at least one row was affected.
	/// </summary>
	/// <typeparam name="T">The type of the value to return on success.</typeparam>
	/// <param name="sql">The SQL UPDATE statement to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL command.</param>
	/// <param name="key">The key of the entity being updated.</param>
	/// <param name="resultSelector">A function that returns the value to include in the successful result.</param>
	/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
	/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<T> UpdateAndReturnAsync<T>(
		string sql,
		object? parameters,
		object key,
		Func<T> resultSelector,
		string uniqueConstraintMessage = "Record already exists",
		string? foreignKeyMessage = "Referenced record does not exist")
		=> new(this, connection.UpdateAsync(sql, parameters, key, resultSelector, uniqueConstraintMessage, foreignKeyMessage, transaction, cancellationToken));

	#endregion

	#region Delete

	/// <summary>
	/// Executes a DELETE command and returns a chainable result if at least one row was affected.
	/// </summary>
	/// <param name="sql">The SQL DELETE statement to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL command.</param>
	/// <param name="key">The key of the entity being deleted.</param>
	/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
	/// <returns>A <see cref="DbResult"/> that can be chained with other database operations.</returns>
	public DbResult DeleteAsync(
		string sql,
		object? parameters,
		object key,
		string foreignKeyMessage = "Cannot delete, record is in use")
		=> new(this, connection.DeleteAsync(sql, parameters, key, foreignKeyMessage, transaction, cancellationToken));

	#endregion

	#region Get

	/// <summary>
	/// Retrieves a single entity by executing the specified SQL query.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the object to be returned.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="key">A key associated with the query result.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<T> GetAsync<T>(
		string sql,
		object key)
		=> new(this, connection.GetAsync<T>(sql, null, key, transaction, cancellationToken));

	/// <summary>
	/// Retrieves a single entity by executing the specified SQL query.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the object to be returned.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="key">A key associated with the query result.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<T> GetAsync<T>(
		string sql,
		object? parameters,
		object key)
		=> new(this, connection.GetAsync<T>(sql, parameters, key, transaction, cancellationToken));

	/// <summary>
	/// Retrieves a single entity by executing the specified SQL query, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the object returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the object in the final result.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="key">A key associated with the query result.</param>
	/// <param name="mapper">A function to transform the data item to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<TModel> GetAsync<TData, TModel>(
		string sql,
		object key,
		Func<TData, TModel> mapper)
		=> new(this, connection.GetAsync(sql, null, key, mapper, transaction, cancellationToken));

	/// <summary>
	/// Retrieves a single entity by executing the specified SQL query, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the object returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the object in the final result.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="key">A key associated with the query result.</param>
	/// <param name="mapper">A function to transform the data item to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<TModel> GetAsync<TData, TModel>(
		string sql,
		object? parameters,
		object key,
		Func<TData, TModel> mapper)
		=> new(this, connection.GetAsync(sql, parameters, key, mapper, transaction, cancellationToken));

	#endregion

	#region GetScalar

	/// <summary>
	/// Executes the specified SQL query and returns the first column of the first row.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the scalar value to return.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<T> GetScalarAsync<T>(string sql)
		=> new(this, connection.GetScalarAsync<T>(sql, null, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the first column of the first row.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the scalar value to return.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<T> GetScalarAsync<T>(string sql, object? parameters)
		=> new(this, connection.GetScalarAsync<T>(sql, parameters, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the first column of the first row, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the scalar value returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the value in the final result.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="mapper">A function to transform the data value to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<TModel> GetScalarAsync<TData, TModel>(string sql, Func<TData?, TModel> mapper)
		=> new(this, connection.GetScalarAsync(sql, null, mapper, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the first column of the first row, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the scalar value returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the value in the final result.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="mapper">A function to transform the data value to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<TModel> GetScalarAsync<TData, TModel>(string sql, object? parameters, Func<TData?, TModel> mapper)
		=> new(this, connection.GetScalarAsync(sql, parameters, mapper, transaction, cancellationToken));

	#endregion

	#region QueryAny

	/// <summary>
	/// Executes the specified SQL query and returns zero or more results as a read-only list.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the elements to be returned.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<IReadOnlyList<T>> QueryAnyAsync<T>(string sql)
		=> new(this, connection.QueryAnyAsync<T>(sql, null, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns zero or more results as a read-only list.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the elements to be returned.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<IReadOnlyList<T>> QueryAnyAsync<T>(string sql, object? parameters)
		=> new(this, connection.QueryAnyAsync<T>(sql, parameters, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns zero or more results, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the elements returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the elements in the final result list.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="mapper">A function to transform each data item to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<IReadOnlyList<TModel>> QueryAnyAsync<TData, TModel>(string sql, Func<TData, TModel> mapper)
		=> new(this, connection.QueryAnyAsync(sql, null, mapper, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns zero or more results, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the elements returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the elements in the final result list.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="mapper">A function to transform each data item to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<IReadOnlyList<TModel>> QueryAnyAsync<TData, TModel>(string sql, object? parameters, Func<TData, TModel> mapper)
		=> new(this, connection.QueryAnyAsync(sql, parameters, mapper, transaction, cancellationToken));

	#endregion

	#region QueryPaged

	/// <summary>
	/// Executes the specified SQL query and returns the results as a paginated result.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the elements to be returned.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="totalCount">The total number of records.</param>
	/// <param name="pageSize">The number of items per page.</param>
	/// <param name="page">The current page number (1-based).</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<PagedResult<T>> QueryPagedAsync<T>(
		string sql,
		int totalCount,
		int pageSize,
		int page)
		=> new(this, connection.QueryPagedAsync<T>(sql, null, totalCount, pageSize, page, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the results as a paginated result.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the elements to be returned.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="totalCount">The total number of records.</param>
	/// <param name="pageSize">The number of items per page.</param>
	/// <param name="page">The current page number (1-based).</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<PagedResult<T>> QueryPagedAsync<T>(
		string sql,
		object? parameters,
		int totalCount,
		int pageSize,
		int page)
		=> new(this, connection.QueryPagedAsync<T>(sql, parameters, totalCount, pageSize, page, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the results as a paginated result, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the elements returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the elements in the final paged result.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="totalCount">The total number of records.</param>
	/// <param name="pageSize">The number of items per page.</param>
	/// <param name="page">The current page number (1-based).</param>
	/// <param name="mapper">A function to transform each data item to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<PagedResult<TModel>> QueryPagedAsync<TData, TModel>(
		string sql,
		int totalCount,
		int pageSize,
		int page,
		Func<TData, TModel> mapper)
		=> new(this, connection.QueryPagedAsync(sql, null, totalCount, pageSize, page, mapper, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the results as a paginated result, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the elements returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the elements in the final paged result.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="totalCount">The total number of records.</param>
	/// <param name="pageSize">The number of items per page.</param>
	/// <param name="page">The current page number (1-based).</param>
	/// <param name="mapper">A function to transform each data item to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<PagedResult<TModel>> QueryPagedAsync<TData, TModel>(
		string sql,
		object? parameters,
		int totalCount,
		int pageSize,
		int page,
		Func<TData, TModel> mapper)
		=> new(this, connection.QueryPagedAsync(sql, parameters, totalCount, pageSize, page, mapper, transaction, cancellationToken));

	#endregion

	#region QueryCursor

	/// <summary>
	/// Executes the specified SQL query and returns the results as a cursor-based paginated result.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the elements to be returned.</typeparam>
	/// <typeparam name="TColumn">The type of the sort column used for cursor positioning.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="pageSize">The maximum number of items to return per page.</param>
	/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<CursorResult<T>> QueryCursorAsync<T, TColumn>(
		string sql,
		int pageSize,
		Func<T, (TColumn Column, Guid Id)> cursorSelector)
		=> new(this, connection.QueryCursorAsync(sql, null, pageSize, cursorSelector, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the results as a cursor-based paginated result.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the elements to be returned.</typeparam>
	/// <typeparam name="TColumn">The type of the sort column used for cursor positioning.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="pageSize">The maximum number of items to return per page.</param>
	/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<CursorResult<T>> QueryCursorAsync<T, TColumn>(
		string sql,
		object? parameters,
		int pageSize,
		Func<T, (TColumn Column, Guid Id)> cursorSelector)
		=> new(this, connection.QueryCursorAsync(sql, parameters, pageSize, cursorSelector, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the results as a cursor-based paginated result, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the elements returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the elements in the final cursor result.</typeparam>
	/// <typeparam name="TColumn">The type of the sort column used for cursor positioning.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="pageSize">The maximum number of items to return per page.</param>
	/// <param name="mapper">A function to transform each data item to the domain model.</param>
	/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<CursorResult<TModel>> QueryCursorAsync<TData, TModel, TColumn>(
		string sql,
		int pageSize,
		Func<TData, TModel> mapper,
		Func<TModel, (TColumn Column, Guid Id)> cursorSelector)
		=> new(this, connection.QueryCursorAsync(sql, null, pageSize, mapper, cursorSelector, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns the results as a cursor-based paginated result, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the elements returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the elements in the final cursor result.</typeparam>
	/// <typeparam name="TColumn">The type of the sort column used for cursor positioning.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="pageSize">The maximum number of items to return per page.</param>
	/// <param name="mapper">A function to transform each data item to the domain model.</param>
	/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<CursorResult<TModel>> QueryCursorAsync<TData, TModel, TColumn>(
		string sql,
		object? parameters,
		int pageSize,
		Func<TData, TModel> mapper,
		Func<TModel, (TColumn Column, Guid Id)> cursorSelector)
		=> new(this, connection.QueryCursorAsync(sql, parameters, pageSize, mapper, cursorSelector, transaction, cancellationToken));

	#endregion

	#region QuerySlice

	/// <summary>
	/// Executes the specified SQL query and returns a slice of results with an indicator for whether more items exist.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the elements to be returned.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="pageSize">The maximum number of items to return.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<SliceResult<T>> QuerySliceAsync<T>(string sql, int pageSize)
		=> new(this, connection.QuerySliceAsync<T>(sql, null, pageSize, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns a slice of results with an indicator for whether more items exist.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="T">The type of the elements to be returned.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="pageSize">The maximum number of items to return.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<SliceResult<T>> QuerySliceAsync<T>(string sql, object? parameters, int pageSize)
		=> new(this, connection.QuerySliceAsync<T>(sql, parameters, pageSize, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns a slice of results, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the elements returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the elements in the final slice result.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="pageSize">The maximum number of items to return.</param>
	/// <param name="mapper">A function to transform each data item to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<SliceResult<TModel>> QuerySliceAsync<TData, TModel>(string sql, int pageSize, Func<TData, TModel> mapper)
		=> new(this, connection.QuerySliceAsync(sql, null, pageSize, mapper, transaction, cancellationToken));

	/// <summary>
	/// Executes the specified SQL query and returns a slice of results, applying a mapping function.
	/// Returns a <see cref="DbResult{T}"/> for fluent chaining within transactions.
	/// </summary>
	/// <typeparam name="TData">The type of the elements returned by the SQL query.</typeparam>
	/// <typeparam name="TModel">The type of the elements in the final slice result.</typeparam>
	/// <param name="sql">The SQL query to execute.</param>
	/// <param name="parameters">An object containing the parameters to be passed to the SQL query.</param>
	/// <param name="pageSize">The maximum number of items to return.</param>
	/// <param name="mapper">A function to transform each data item to the domain model.</param>
	/// <returns>A <see cref="DbResult{T}"/> that can be chained with other database operations.</returns>
	public DbResult<SliceResult<TModel>> QuerySliceAsync<TData, TModel>(string sql, object? parameters, int pageSize, Func<TData, TModel> mapper)
		=> new(this, connection.QuerySliceAsync(sql, parameters, pageSize, mapper, transaction, cancellationToken));

	#endregion

}
