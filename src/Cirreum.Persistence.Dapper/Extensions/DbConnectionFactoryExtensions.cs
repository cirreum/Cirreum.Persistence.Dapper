namespace Cirreum.Persistence;

using System.Data;

public static class DbConnectionFactoryExtensions {

	extension(IDbConnectionFactory factory) {

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
			return await action(connection);
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
			return await action(connection);
		}

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
		public async Task<Result> ExecuteInTransactionAsync(
			Func<TransactionContext, Task<Result>> operations,
			CancellationToken cancellationToken = default) {

			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.ExecuteInTransactionAsync(operations, cancellationToken);
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
		public async Task<Result<T>> ExecuteInTransactionAsync<T>(
			Func<TransactionContext, Task<Result<T>>> operations,
			CancellationToken cancellationToken = default) {

			await using var connection = await factory.CreateConnectionAsync(cancellationToken);
			return await connection.ExecuteInTransactionAsync(operations, cancellationToken);
		}

	}

}