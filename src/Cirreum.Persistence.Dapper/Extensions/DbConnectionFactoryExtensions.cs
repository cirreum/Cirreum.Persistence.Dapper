namespace Cirreum.Persistence;

using Cirreum.Exceptions;
using Microsoft.Data.SqlClient;
using System;
using System.Data;

/// <summary>
/// Provides extension methods for executing asynchronous operations and transactions using an <see
/// cref="IDbConnectionFactory"/> instance.
/// </summary>
/// <remarks>These extension methods simplify the process of managing database connections and transactions by
/// ensuring proper resource handling and encapsulating common patterns for asynchronous execution. All methods ensure
/// that connections are opened, disposed, and, when applicable, that transactions are committed or rolled back
/// appropriately. Callers are responsible for handling the results and any errors encapsulated within the returned <see
/// cref="Result"/> or <see cref="Result{T}"/> objects.</remarks>
public static class DbConnectionFactoryExtensions {

	extension(IDbConnectionFactory factory) {

		#region EXECUTE ASYNC

		/// <summary>
		/// Executes the specified asynchronous action using a database connection created by the factory.
		/// </summary>
		/// <param name="action">A delegate that defines the asynchronous operation to perform with the provided database connection. The delegate
		/// receives an open <see cref="IDbConnection"/> and returns a <see cref="Task{Result}"/> representing the operation.</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. The default value is <see
		/// cref="CancellationToken.None"/>.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains the <see cref="Result"/> returned by
		/// the specified action.</returns>
		public async Task<Result> ExecuteAsync(
			Func<IDbConnection, Task<Result>> action,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			try {
				return await action(connection);
			} catch (SqlException ex) {
				return ex.ToResult();
			} catch (Exception ex) {
				return Result.Fail(ex);
			}
		}

		/// <summary>
		/// Executes the specified asynchronous database action using a managed database connection.
		/// </summary>
		/// <remarks>The method ensures that the database connection is properly opened and disposed of for the
		/// duration of the action. The caller is responsible for handling the result and any errors encapsulated within the
		/// <see cref="Result{T}"/>.</remarks>
		/// <typeparam name="T">The type of the result returned by the database action.</typeparam>
		/// <param name="action">A function that performs an asynchronous operation using an open database connection and returns a result of type
		/// <typeparamref name="T"/>.</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation. The default value is <see
		/// cref="CancellationToken.None"/>.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/> representing
		/// the outcome of the database action.</returns>
		public async Task<Result<T>> ExecuteAsync<T>(
			Func<IDbConnection, Task<Result<T>>> action,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			try {
				return await action(connection);
			} catch (SqlException ex) {
				return ex.ToResult<T>();
			} catch (Exception ex) {
				return Result.Fail<T>(ex);
			}
		}

		#endregion

		#region EXECUTE TRANSACTION ASYNC

		/// <summary>
		/// Executes the specified set of operations within a database transaction asynchronously.
		/// </summary>
		/// <remarks>If the operations delegate completes successfully, the transaction is committed; otherwise, it is
		/// rolled back. The transaction is automatically disposed when the operation completes.</remarks>
		/// <param name="operations">A delegate that defines the operations to execute within the transaction. The delegate receives a
		/// TransactionContext and returns a Task that produces a Result.</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a Result indicating the outcome of the
		/// transaction.</returns>
		public async Task<Result> ExecuteTransactionAsync(
			Func<TransactionContext, Task<Result>> operations,
			CancellationToken cancellationToken = default) {

			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.ExecuteTransactionAsync(operations, cancellationToken);
		}

		/// <summary>
		/// Executes the specified set of operations within a database transaction asynchronously.
		/// </summary>
		/// <remarks>If the operations complete successfully, the transaction is committed. If an exception is thrown
		/// or the result indicates failure, the transaction is rolled back. The method ensures that the connection is
		/// properly disposed after execution.</remarks>
		/// <typeparam name="T">The type of the result produced by the transactional operations.</typeparam>
		/// <param name="operations">A delegate that defines the operations to execute within the transaction. The delegate receives a
		/// TransactionContext and returns a task that produces a Result of type T.</param>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a Result of type T produced by the
		/// transactional operations.</returns>
		public async Task<Result<T>> ExecuteTransactionAsync<T>(
			Func<TransactionContext, Task<Result<T>>> operations,
			CancellationToken cancellationToken = default) {

			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.ExecuteTransactionAsync(operations, cancellationToken);
		}

		#endregion

		#region GET

		/// <summary>
		/// Retrieves a single entity by executing the specified SQL query asynchronously and returns the result wrapped in a <see cref="Result{T}"/>
		/// object.
		/// </summary>
		/// <typeparam name="T">The type of the object to be returned from the query.</typeparam>
		/// <param name="sql">The SQL query to execute. Should be a statement that returns a single row.</param>
		/// <param name="key">A key associated with the query result, used to identify or correlate the returned value.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation. The result contains a <see cref="Result{T}"/> object with the
		/// queried value, or a NotFound result if no row is found.</returns>
		public async Task<Result<T>> GetAsync<T>(
			string sql,
			object key,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.GetAsync<T>(sql, null, key, null, cancellationToken);
		}

		/// <summary>
		/// Retrieves a single entity by executing the specified SQL query asynchronously and returns the result wrapped in a <see cref="Result{T}"/>
		/// object.
		/// </summary>
		/// <typeparam name="T">The type of the object to be returned from the query.</typeparam>
		/// <param name="sql">The SQL query to execute. Should be a statement that returns a single row.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are
		/// required.</param>
		/// <param name="key">A key associated with the query result, used to identify or correlate the returned value.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation. The result contains a <see cref="Result{T}"/> object with the
		/// queried value, or a NotFound result if no row is found.</returns>
		public async Task<Result<T>> GetAsync<T>(
			string sql,
			object? parameters,
			object key,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.GetAsync<T>(
				sql,
				parameters,
				key,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Retrieves a single entity by executing the specified SQL query asynchronously and returns the result wrapped in a <see cref="Result{T}"/>
		/// object, applying a mapping function to transform the item.
		/// </summary>
		/// <typeparam name="TData">The type of the object returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the object in the final result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute. Should be a statement that returns a single row.</param>
		/// <param name="key">A key associated with the query result, used to identify or correlate the returned value.</param>
		/// <param name="mapper">A function to transform the data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation. The result contains a <see cref="Result{T}"/> object with the
		/// mapped value, or a NotFound result if no row is found.</returns>
		public async Task<Result<TModel>> GetAsync<TData, TModel>(
			string sql,
			object key,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.GetAsync(sql, null, key, mapper, null, cancellationToken);
		}

		/// <summary>
		/// Retrieves a single entity by executing the specified SQL query asynchronously and returns the result wrapped in a <see cref="Result{T}"/>
		/// object, applying a mapping function to transform the item.
		/// </summary>
		/// <typeparam name="TData">The type of the object returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the object in the final result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute. Should be a statement that returns a single row.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="key">A key associated with the query result, used to identify or correlate the returned value.</param>
		/// <param name="mapper">A function to transform the data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task representing the asynchronous operation. The result contains a <see cref="Result{T}"/> object with the
		/// mapped value, or a NotFound result if no row is found.</returns>
		public async Task<Result<TModel>> GetAsync<TData, TModel>(
			string sql,
			object? parameters,
			object key,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.GetAsync(
				sql,
				parameters,
				key,
				mapper,
				transaction: null,
				cancellationToken: cancellationToken);
		}


		#endregion

		#region GET SCALAR

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the first column of the first row
		/// in the result set, wrapped in a <see cref="Result{T}"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method is useful for queries that return a single scalar value, such as COUNT, SUM, MAX,
		/// or selecting a single column value.
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// SELECT COUNT(*) FROM Orders WHERE CustomerId = @CustomerId
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await factory.GetScalarAsync&lt;int&gt;(
		///     "SELECT COUNT(*) FROM Orders",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <typeparam name="T">The type of the scalar value to return.</typeparam>
		/// <param name="sql">The SQL query to execute. Should return a single scalar value.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// with the scalar value.</returns>
		public async Task<Result<T>> GetScalarAsync<T>(
			string sql,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.GetScalarAsync<T>(sql, null, null, cancellationToken);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the first column of the first row
		/// in the result set, wrapped in a <see cref="Result{T}"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method is useful for queries that return a single scalar value, such as COUNT, SUM, MAX,
		/// or selecting a single column value.
		/// </para>
		/// <para>
		/// <strong>SQL Pattern:</strong>
		/// </para>
		/// <code>
		/// SELECT COUNT(*) FROM Orders WHERE CustomerId = @CustomerId
		/// </code>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await factory.GetScalarAsync&lt;int&gt;(
		///     "SELECT COUNT(*) FROM Orders WHERE CustomerId = @CustomerId",
		///     new { query.CustomerId },
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <typeparam name="T">The type of the scalar value to return.</typeparam>
		/// <param name="sql">The SQL query to execute. Should return a single scalar value.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// with the scalar value.</returns>
		public async Task<Result<T>> GetScalarAsync<T>(
			string sql,
			object? parameters,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.GetScalarAsync<T>(
				sql,
				parameters,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the first column of the first row
		/// in the result set, applying a mapping function to transform the value, wrapped in a <see cref="Result{T}"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method is useful for queries that return a single scalar value that needs transformation,
		/// such as converting database types to domain types.
		/// </para>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await factory.GetScalarAsync&lt;int, OrderCount&gt;(
		///     "SELECT COUNT(*) FROM Orders WHERE CustomerId = @CustomerId",
		///     new { query.CustomerId },
		///     count =&gt; new OrderCount(count),
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <typeparam name="TData">The type of the scalar value returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the value in the final result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute. Should return a single scalar value.</param>
		/// <param name="mapper">A function to transform the data value to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// with the mapped scalar value.</returns>
		public async Task<Result<TModel>> GetScalarAsync<TData, TModel>(
			string sql,
			Func<TData?, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.GetScalarAsync(
				sql,
				null,
				mapper,
				null,
				cancellationToken);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the first column of the first row
		/// in the result set, applying a mapping function to transform the value, wrapped in a <see cref="Result{T}"/>.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method is useful for queries that return a single scalar value that needs transformation,
		/// such as converting database types to domain types.
		/// </para>
		/// <para>
		/// <strong>Usage Pattern:</strong>
		/// </para>
		/// <code>
		/// return await factory.GetScalarAsync&lt;int, OrderCount&gt;(
		///     "SELECT COUNT(*) FROM Orders WHERE CustomerId = @CustomerId",
		///     new { query.CustomerId },
		///     count =&gt; new OrderCount(count),
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <typeparam name="TData">The type of the scalar value returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the value in the final result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute. Should return a single scalar value.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="mapper">A function to transform the data value to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// with the mapped scalar value.</returns>
		public async Task<Result<TModel>> GetScalarAsync<TData, TModel>(
			string sql,
			object? parameters,
			Func<TData?, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.GetScalarAsync(
				sql,
				parameters,
				mapper,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		#endregion

		#region QUERY ANY

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns zero or more results as a read-only list
		/// wrapped in a successful Result.
		/// </summary>
		/// <typeparam name="T">The type of the elements to be returned in the result list.</typeparam>
		/// <param name="sql">The SQL query to execute against the database.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful Result
		/// object wrapping a read-only list of items (which may be empty).</returns>
		public async Task<Result<IReadOnlyList<T>>> QueryAnyAsync<T>(
			string sql,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryAnyAsync<T>(sql, null, null, cancellationToken);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns zero or more results as a read-only list
		/// wrapped in a successful Result.
		/// </summary>
		/// <typeparam name="T">The type of the elements to be returned in the result list.</typeparam>
		/// <param name="sql">The SQL query to execute against the database.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or null if no parameters are required.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful Result
		/// object wrapping a read-only list of items (which may be empty).</returns>
		public async Task<Result<IReadOnlyList<T>>> QueryAnyAsync<T>(
			string sql,
			object? parameters,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryAnyAsync<T>(
				sql,
				parameters,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns zero or more results as a read-only list
		/// wrapped in a successful Result, applying a mapping function to transform each item.
		/// </summary>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final result list (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute against the database.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful Result
		/// object wrapping a read-only list of mapped items (which may be empty).</returns>
		public async Task<Result<IReadOnlyList<TModel>>> QueryAnyAsync<TData, TModel>(
			string sql,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryAnyAsync(sql, null, mapper, null, cancellationToken);
		}
		/// <summary>
		/// Executes the specified SQL query asynchronously and returns zero or more results as a read-only list
		/// wrapped in a successful Result, applying a mapping function to transform each item.
		/// </summary>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final result list (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute against the database.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or null if no parameters are required.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful Result
		/// object wrapping a read-only list of mapped items (which may be empty).</returns>
		public async Task<Result<IReadOnlyList<TModel>>> QueryAnyAsync<TData, TModel>(
			string sql,
			object? parameters,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryAnyAsync(
				sql,
				parameters,
				mapper,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		#endregion

		#region QUERY PAGED

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
		/// <param name="totalCount">The total number of records matching the query criteria (before pagination).</param>
		/// <param name="pageSize">The number of items per page.</param>
		/// <param name="page">The current page number (1-based).</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="PagedResult{T}"/> with the queried items and pagination metadata.</returns>
		public async Task<Result<PagedResult<T>>> QueryPagedAsync<T>(
			string sql,
			int totalCount,
			int pageSize,
			int page,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryPagedAsync<T>(
				sql,
				null,
				totalCount,
				pageSize,
				page,
				null,
				cancellationToken);
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
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="totalCount">The total number of records matching the query criteria (before pagination).</param>
		/// <param name="pageSize">The number of items per page.</param>
		/// <param name="page">The current page number (1-based).</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="PagedResult{T}"/> with the queried items and pagination metadata.</returns>
		public async Task<Result<PagedResult<T>>> QueryPagedAsync<T>(
			string sql,
			object? parameters,
			int totalCount,
			int pageSize,
			int page,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryPagedAsync<T>(
				sql,
				parameters,
				totalCount,
				pageSize,
				page,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the results as a paginated result wrapped in a
		/// <see cref="Result{T}"/> object, applying a mapping function to transform each item.
		/// </summary>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final paged result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute against the database. Should include OFFSET/FETCH for pagination.</param>
		/// <param name="totalCount">The total number of records matching the query criteria (before pagination).</param>
		/// <param name="pageSize">The number of items per page.</param>
		/// <param name="page">The current page number (1-based).</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="PagedResult{T}"/> with the mapped items and pagination metadata.</returns>
		public async Task<Result<PagedResult<TModel>>> QueryPagedAsync<TData, TModel>(
			string sql,
			int totalCount,
			int pageSize,
			int page,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryPagedAsync(
				sql,
				null,
				totalCount,
				pageSize,
				page,
				mapper,
				null,
				cancellationToken);
		}

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the results as a paginated result wrapped in a
		/// <see cref="Result{T}"/> object, applying a mapping function to transform each item.
		/// </summary>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final paged result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute against the database. Should include OFFSET/FETCH for pagination.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="totalCount">The total number of records matching the query criteria (before pagination).</param>
		/// <param name="pageSize">The number of items per page.</param>
		/// <param name="page">The current page number (1-based).</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="PagedResult{T}"/> with the mapped items and pagination metadata.</returns>
		public async Task<Result<PagedResult<TModel>>> QueryPagedAsync<TData, TModel>(
			string sql,
			object? parameters,
			int totalCount,
			int pageSize,
			int page,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryPagedAsync(
				sql,
				parameters,
				totalCount,
				pageSize,
				page,
				mapper,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		#endregion

		#region QUERY CURSOR

		/// <summary>
		/// Executes the specified SQL query asynchronously and returns the results as a cursor-based paginated result
		/// wrapped in a <see cref="Result{T}"/> object.
		/// </summary>
		/// <remarks>
		/// <para>
		/// This method automatically injects a <c>@PageSize</c> parameter set to <paramref name="pageSize"/> + 1 to
		/// determine if additional pages exist. Your SQL query should use <c>TOP (@PageSize)</c>.
		/// </para>
		/// </remarks>
		/// <typeparam name="T">The type of the elements to be returned in the cursor result.</typeparam>
		/// <typeparam name="TColumn">The type of the sort column used for cursor positioning.</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="pageSize">The maximum number of items to return per page.</param>
		/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier from an item for cursor encoding.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="CursorResult{T}"/> with the queried items and cursor metadata.</returns>
		public async Task<Result<CursorResult<T>>> QueryCursorAsync<T, TColumn>(
			string sql,
			int pageSize,
			Func<T, (TColumn Column, Guid Id)> cursorSelector,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryCursorAsync(
				sql,
				null,
				pageSize,
				cursorSelector,
				null,
				cancellationToken);
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
		/// return await factory.QueryCursorAsync&lt;Order, DateTime&gt;(
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
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="pageSize">The maximum number of items to return per page.</param>
		/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier from an item for cursor encoding.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="CursorResult{T}"/> with the queried items and cursor metadata.</returns>
		public async Task<Result<CursorResult<T>>> QueryCursorAsync<T, TColumn>(
			string sql,
			object? parameters,
			int pageSize,
			Func<T, (TColumn Column, Guid Id)> cursorSelector,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryCursorAsync(
				sql,
				parameters,
				pageSize,
				cursorSelector,
				transaction: null,
				cancellationToken: cancellationToken);
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
		/// </remarks>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final cursor result (domain layer).</typeparam>
		/// <typeparam name="TColumn">The type of the sort column used for cursor positioning.</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="pageSize">The maximum number of items to return per page.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier from a mapped item for cursor encoding.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="CursorResult{T}"/> with the mapped items and cursor metadata.</returns>
		public async Task<Result<CursorResult<TModel>>> QueryCursorAsync<TData, TModel, TColumn>(
			string sql,
			int pageSize,
			Func<TData, TModel> mapper,
			Func<TModel, (TColumn Column, Guid Id)> cursorSelector,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryCursorAsync(
				sql,
				pageSize,
				mapper,
				cursorSelector,
				transaction: null,
				cancellationToken: cancellationToken);
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
		/// return await factory.QueryCursorAsync&lt;OrderData, Order, DateTime&gt;(
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
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="pageSize">The maximum number of items to return per page.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cursorSelector">A function that extracts the sort column value and unique identifier from a mapped item for cursor encoding.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="CursorResult{T}"/> with the mapped items and cursor metadata.</returns>
		public async Task<Result<CursorResult<TModel>>> QueryCursorAsync<TData, TModel, TColumn>(
			string sql,
			object? parameters,
			int pageSize,
			Func<TData, TModel> mapper,
			Func<TModel, (TColumn Column, Guid Id)> cursorSelector,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QueryCursorAsync(
				sql,
				parameters,
				pageSize,
				mapper,
				cursorSelector,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		#endregion

		#region QUERY SLICE

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
		/// return await factory.QuerySliceAsync&lt;Order&gt;(
		///     "SELECT TOP (@PageSize) ... ORDER BY CreatedAt DESC",
		///     query.PageSize,
		///     cancellationToken);
		/// </code>
		/// </remarks>
		/// <typeparam name="T">The type of the elements to be returned in the slice result.</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="pageSize">The maximum number of items to return.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="SliceResult{T}"/> with the queried items and a flag indicating if more items exist.</returns>
		public async Task<Result<SliceResult<T>>> QuerySliceAsync<T>(
			string sql,
			int pageSize,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QuerySliceAsync<T>(
				sql,
				pageSize,
				transaction: null,
				cancellationToken: cancellationToken);
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
		/// return await factory.QuerySliceAsync&lt;Order&gt;(
		///     "SELECT TOP (@PageSize) ... ORDER BY CreatedAt DESC",
		///     new { query.CustomerId },
		///     query.PageSize,
		///     cancellationToken);
		/// </code>
		/// <para>
		/// Use this for simple "load more" patterns without full pagination metadata.
		/// For stable cursor-based pagination, use <see cref="QueryCursorAsync{T, TColumn}(IDbConnectionFactory, string, object?, int, Func{T, ValueTuple{TColumn, Guid}}, CancellationToken)"/> instead.
		/// For full pagination with total counts, use <see cref="QueryPagedAsync{T}(IDbConnectionFactory, string, object?, int, int, int, CancellationToken)"/> instead.
		/// </para>
		/// </remarks>
		/// <typeparam name="T">The type of the elements to be returned in the slice result.</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="pageSize">The maximum number of items to return.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="SliceResult{T}"/> with the queried items and a flag indicating if more items exist.</returns>
		public async Task<Result<SliceResult<T>>> QuerySliceAsync<T>(
			string sql,
			object? parameters,
			int pageSize,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QuerySliceAsync<T>(
				sql,
				parameters,
				pageSize,
				transaction: null,
				cancellationToken: cancellationToken);
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
		/// </remarks>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final slice result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="pageSize">The maximum number of items to return.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="SliceResult{T}"/> with the mapped items and a flag indicating if more items exist.</returns>
		public async Task<Result<SliceResult<TModel>>> QuerySliceAsync<TData, TModel>(
			string sql,
			int pageSize,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QuerySliceAsync(
				sql,
				pageSize,
				mapper,
				transaction: null,
				cancellationToken: cancellationToken);
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
		/// return await factory.QuerySliceAsync&lt;OrderData, Order&gt;(
		///     "SELECT TOP (@PageSize) ... ORDER BY CreatedAt DESC",
		///     new { query.CustomerId },
		///     query.PageSize,
		///     data =&gt; new Order(data),
		///     cancellationToken);
		/// </code>
		/// <para>
		/// Use this for simple "load more" patterns without full pagination metadata.
		/// For stable cursor-based pagination, use <see cref="QueryCursorAsync{TData, TModel, TColumn}(IDbConnectionFactory, string, object?, int, Func{TData, TModel}, Func{TModel, ValueTuple{TColumn, Guid}}, CancellationToken)"/> instead.
		/// For full pagination with total counts, use <see cref="QueryPagedAsync{TData, TModel}(IDbConnectionFactory, string, object?, int, int, int, Func{TData, TModel}, CancellationToken)"/> instead.
		/// </para>
		/// </remarks>
		/// <typeparam name="TData">The type of the elements returned by the SQL query (data layer).</typeparam>
		/// <typeparam name="TModel">The type of the elements in the final slice result (domain layer).</typeparam>
		/// <param name="sql">The SQL query to execute. Should use <c>TOP (@PageSize)</c>.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL query, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="pageSize">The maximum number of items to return.</param>
		/// <param name="mapper">A function to transform each data item to the domain model.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// wrapping a <see cref="SliceResult{T}"/> with the mapped items and a flag indicating if more items exist.</returns>
		public async Task<Result<SliceResult<TModel>>> QuerySliceAsync<TData, TModel>(
			string sql,
			object? parameters,
			int pageSize,
			Func<TData, TModel> mapper,
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.QuerySliceAsync(
				sql,
				parameters,
				pageSize,
				mapper,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		#endregion

		#region INSERT

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
		/// return await factory.InsertAsync(
		///     "INSERT INTO Orders (OrderId, CustomerId, Amount) VALUES (@OrderId, @CustomerId, @Amount)",
		///     new { OrderId = Guid.CreateVersion7(), command.CustomerId, command.Amount },
		///     uniqueConstraintMessage: "Order already exists",
		///     foreignKeyMessage: "Customer not found",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <param name="sql">The SQL INSERT statement to execute.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs, or <see langword="null"/> to let the exception propagate.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful <see cref="Result"/>
		/// or a failure result with an appropriate exception.</returns>
		public async Task<Result> InsertAsync(
			string sql,
			object? parameters = null,
			string uniqueConstraintMessage = "Record already exists",
			string? foreignKeyMessage = "Referenced record does not exist",
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.InsertAsync(
				sql,
				parameters,
				uniqueConstraintMessage,
				foreignKeyMessage,
				transaction: null,
				cancellationToken: cancellationToken);
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
		/// return await factory.InsertAndReturnAsync(
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
		/// <param name="parameters">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="resultSelector">A function that returns the value to include in the successful result.</param>
		/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs, or <see langword="null"/> to let the exception propagate.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// with the value from <paramref name="resultSelector"/> on success, or a failure result with an appropriate exception.</returns>
		public async Task<Result<T>> InsertAndReturnAsync<T>(
			string sql,
			object? parameters,
			Func<T> resultSelector,
			string uniqueConstraintMessage = "Record already exists",
			string? foreignKeyMessage = "Referenced record does not exist",
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.InsertAndReturnAsync(
				sql,
				parameters,
				resultSelector,
				uniqueConstraintMessage,
				foreignKeyMessage,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		#endregion

		#region UPDATE

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
		/// return await factory.UpdateAsync(
		///     "UPDATE Orders SET Amount = @Amount WHERE OrderId = @OrderId",
		///     new { command.OrderId, command.Amount },
		///     key: command.OrderId,
		///     uniqueConstraintMessage: "Order with this reference already exists",
		///     foreignKeyMessage: "Customer not found",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <param name="sql">The SQL UPDATE statement to execute.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="key">The key of the entity being updated, used in the <see cref="NotFoundException"/> if no rows are affected.</param>
		/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs, or <see langword="null"/> to let the exception propagate.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful <see cref="Result"/>
		/// if at least one row was updated, or a failure result with an appropriate exception.</returns>
		public async Task<Result> UpdateAsync(
			string sql,
			object? parameters,
			object key,
			string uniqueConstraintMessage = "Record already exists",
			string? foreignKeyMessage = "Referenced record does not exist",
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.UpdateAsync(
				sql,
				parameters,
				key,
				uniqueConstraintMessage,
				foreignKeyMessage,
				transaction: null,
				cancellationToken: cancellationToken);
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
		/// return await factory.UpdateAndReturnAsync(
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
		/// <param name="parameters">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="key">The key of the entity being updated, used in the <see cref="NotFoundException"/> if no rows are affected.</param>
		/// <param name="resultSelector">A function that returns the value to include in the successful result.</param>
		/// <param name="uniqueConstraintMessage">The error message to use if a unique constraint violation occurs.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs, or <see langword="null"/> to let the exception propagate.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="Result{T}"/>
		/// with the value from <paramref name="resultSelector"/> if at least one row was updated, or a failure result with an appropriate exception.</returns>
		public async Task<Result<T>> UpdateAndReturnAsync<T>(
			string sql,
			object? parameters,
			object key,
			Func<T> resultSelector,
			string uniqueConstraintMessage = "Record already exists",
			string? foreignKeyMessage = "Referenced record does not exist",
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.UpdateAndReturnAsync(
				sql,
				parameters,
				key,
				resultSelector,
				uniqueConstraintMessage,
				foreignKeyMessage,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		#endregion

		#region DELETE

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
		/// return await factory.DeleteAsync(
		///     "DELETE FROM Orders WHERE OrderId = @OrderId",
		///     new { command.OrderId },
		///     key: command.OrderId,
		///     foreignKeyMessage: "Cannot delete order, it has associated line items",
		///     cancellationToken: cancellationToken);
		/// </code>
		/// </remarks>
		/// <param name="sql">The SQL DELETE statement to execute.</param>
		/// <param name="parameters">An object containing the parameters to be passed to the SQL command, or <see langword="null"/> if no parameters are required.</param>
		/// <param name="key">The key of the entity being deleted, used in the <see cref="NotFoundException"/> if no rows are affected.</param>
		/// <param name="foreignKeyMessage">The error message to use if a foreign key violation occurs.</param>
		/// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
		/// <returns>A task that represents the asynchronous operation. The task result contains a successful <see cref="Result"/>
		/// if at least one row was deleted, or a failure result with an appropriate exception.</returns>
		public async Task<Result> DeleteAsync(
			string sql,
			object? parameters,
			object key,
			string foreignKeyMessage = "Cannot delete, record is in use",
			CancellationToken cancellationToken = default) {
			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.DeleteAsync(
				sql,
				parameters,
				key,
				foreignKeyMessage,
				transaction: null,
				cancellationToken: cancellationToken);
		}

		#endregion

	}

}