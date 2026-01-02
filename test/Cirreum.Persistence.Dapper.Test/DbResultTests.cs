namespace Cirreum.Persistence.Dapper.Test;

using Cirreum;
using Cirreum.Exceptions;

[TestClass]
public sealed class DbResultTests {

	#region Error Propagation

	[TestMethod]
	public async Task DbResult_WhenFailure_PropagatesErrorThroughMapAsync() {
		// Arrange
		var error = new NotFoundException("Not found");
		var failedResult = Task.FromResult(Result.Fail<int>(error));
		var dbResult = new DbResult<int>(default, failedResult);

		// Act
		var mapped = dbResult.MapAsync(x => x * 2);
		var result = await mapped;

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(error, result.Error);
	}

	[TestMethod]
	public async Task DbResult_WhenFailure_PropagatesErrorThroughWhereAsync() {
		// Arrange
		var error = new BadRequestException("Bad request");
		var failedResult = Task.FromResult(Result.Fail<string>(error));
		var dbResult = new DbResult<string>(default, failedResult);

		// Act
		var filtered = dbResult.WhereAsync(x => x.Length > 5, new NotFoundException("Should not see this"));
		var result = await filtered;

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(error, result.Error);
	}

	[TestMethod]
	public async Task DbResult_WhenSuccess_MapAsyncTransformsValue() {
		// Arrange
		var successResult = Task.FromResult(Result.From(42));
		var dbResult = new DbResult<int>(default, successResult);

		// Act
		var mapped = dbResult.MapAsync(x => x * 2);
		var result = await mapped;

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(84, result.Value);
	}

	[TestMethod]
	public async Task DbResult_WhenSuccess_AsyncMapAsyncTransformsValue() {
		// Arrange
		var successResult = Task.FromResult(Result.From(42));
		var dbResult = new DbResult<int>(default, successResult);

		// Act
		var mapped = dbResult.MapAsync(async x => {
			await Task.Delay(1, this.TestContext.CancellationToken);
			return x * 2;
		});
		var result = await mapped;

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(84, result.Value);
	}

	#endregion

	#region WhereAsync

	[TestMethod]
	public async Task DbResult_WhereAsync_WhenPredicateTrue_ReturnsSuccess() {
		// Arrange
		var successResult = Task.FromResult(Result.From("Hello"));
		var dbResult = new DbResult<string>(default, successResult);

		// Act
		var filtered = dbResult.WhereAsync(x => x.Length == 5, new BadRequestException("Wrong length"));
		var result = await filtered;

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Hello", result.Value);
	}

	[TestMethod]
	public async Task DbResult_WhereAsync_WhenPredicateFalse_ReturnsProvidedError() {
		// Arrange
		var successResult = Task.FromResult(Result.From("Hi"));
		var dbResult = new DbResult<string>(default, successResult);
		var expectedError = new BadRequestException("Wrong length");

		// Act
		var filtered = dbResult.WhereAsync(x => x.Length == 5, expectedError);
		var result = await filtered;

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(expectedError, result.Error);
	}

	[TestMethod]
	public async Task DbResult_WhereAsync_WithAsyncPredicate_WhenTrue_ReturnsSuccess() {
		// Arrange
		var successResult = Task.FromResult(Result.From(10));
		var dbResult = new DbResult<int>(default, successResult);

		// Act
		var filtered = dbResult.WhereAsync(
			async x => {
				await Task.Delay(1, this.TestContext.CancellationToken);
				return x > 5;
			},
			new BadRequestException("Too small"));
		var result = await filtered;

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(10, result.Value);
	}

	[TestMethod]
	public async Task DbResult_WhereAsync_WithAsyncPredicate_WhenFalse_ReturnsError() {
		// Arrange
		var successResult = Task.FromResult(Result.From(3));
		var dbResult = new DbResult<int>(default, successResult);
		var expectedError = new BadRequestException("Too small");

		// Act
		var filtered = dbResult.WhereAsync(
			async x => {
				await Task.Delay(1, this.TestContext.CancellationToken);
				return x > 5;
			},
			expectedError);
		var result = await filtered;

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(expectedError, result.Error);
	}

	#endregion

	#region Implicit Conversion

	[TestMethod]
	public async Task DbResult_ImplicitlyConvertsToTaskOfResult() {
		// Arrange
		var successResult = Task.FromResult(Result.From(42));
		var dbResult = new DbResult<int>(default, successResult);

		// Act
		Task<Result<int>> task = dbResult; // implicit conversion
		var result = await task;

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual(42, result.Value);
	}

	[TestMethod]
	public async Task DbResult_CanBeAwaitedDirectly() {
		// Arrange
		var successResult = Task.FromResult(Result.From("test"));
		var dbResult = new DbResult<string>(default, successResult);

		// Act
		var result = await dbResult; // uses GetAwaiter

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("test", result.Value);
	}

	#endregion

	#region Chaining Multiple Operations

	[TestMethod]
	public async Task DbResult_ChainingMapAndWhere_Success() {
		// Arrange
		var successResult = Task.FromResult(Result.From(10));
		var dbResult = new DbResult<int>(default, successResult);

		// Act
		var result = await dbResult
			.MapAsync(x => x * 2)
			.WhereAsync(x => x > 15, new BadRequestException("Too small"))
			.MapAsync(x => $"Value: {x}");

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.AreEqual("Value: 20", result.Value);
	}

	[TestMethod]
	public async Task DbResult_ChainingMapAndWhere_FailsAtWhere() {
		// Arrange
		var successResult = Task.FromResult(Result.From(5));
		var dbResult = new DbResult<int>(default, successResult);
		var expectedError = new BadRequestException("Too small");

		// Act
		var result = await dbResult
			.MapAsync(x => x * 2)  // 5 * 2 = 10
			.WhereAsync(x => x > 15, expectedError) // 10 > 15 is false
			.MapAsync(x => $"Value: {x}"); // should not execute

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(expectedError, result.Error);
	}

	[TestMethod]
	public async Task DbResult_ChainingWithInitialFailure_PropagatesError() {
		// Arrange
		var initialError = new NotFoundException("Initial failure");
		var failedResult = Task.FromResult(Result.Fail<int>(initialError));
		var dbResult = new DbResult<int>(default, failedResult);
		var mapperCalled = false;
		var predicateCalled = false;

		// Act
		var result = await dbResult
			.MapAsync(x => {
				mapperCalled = true;
				return x * 2;
			})
			.WhereAsync(x => {
				predicateCalled = true;
				return x > 15;
			}, new BadRequestException("Too small"))
			.MapAsync(x => $"Value: {x}");

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(initialError, result.Error);
		Assert.IsFalse(mapperCalled, "Mapper should not be called when initial result is failure");
		Assert.IsFalse(predicateCalled, "Predicate should not be called when initial result is failure");
	}

	#endregion

	#region ThenAsync (to non-generic Result)

	[TestMethod]
	public async Task DbResult_ThenAsync_WhenSuccess_ExecutesNext() {
		// Arrange
		var successResult = Task.FromResult(Result.From(42));
		var dbResult = new DbResult<int>(default, successResult);
		var nextExecuted = false;

		// Act
		var result = await dbResult.ThenAsync(async x => {
			await Task.Delay(1, this.TestContext.CancellationToken);
			nextExecuted = true;
			Assert.AreEqual(42, x);
			return Result.Success;
		});

		// Assert
		Assert.IsTrue(result.IsSuccess);
		Assert.IsTrue(nextExecuted);
	}

	[TestMethod]
	public async Task DbResult_ThenAsync_WhenFailure_DoesNotExecuteNext() {
		// Arrange
		var error = new NotFoundException("Not found");
		var failedResult = Task.FromResult(Result.Fail<int>(error));
		var dbResult = new DbResult<int>(default, failedResult);
		var nextExecuted = false;

		// Act
		var result = await dbResult.ThenAsync(async _ => {
			await Task.Delay(1, this.TestContext.CancellationToken);
			nextExecuted = true;
			return Result.Success;
		});

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(error, result.Error);
		Assert.IsFalse(nextExecuted);
	}

	public TestContext TestContext { get; set; }

	#endregion

}

[TestClass]
public sealed class DbResultNonGenericTests {

	#region Implicit Conversion

	[TestMethod]
	public async Task DbResultNonGeneric_ImplicitlyConvertsToTaskOfResult() {
		// Arrange
		var successResult = Task.FromResult(Result.Success);
		var dbResult = new DbResult(default, successResult);

		// Act
		Task<Result> task = dbResult; // implicit conversion
		var result = await task;

		// Assert
		Assert.IsTrue(result.IsSuccess);
	}

	[TestMethod]
	public async Task DbResultNonGeneric_CanBeAwaitedDirectly() {
		// Arrange
		var successResult = Task.FromResult(Result.Success);
		var dbResult = new DbResult(default, successResult);

		// Act
		var result = await dbResult; // uses GetAwaiter

		// Assert
		Assert.IsTrue(result.IsSuccess);
	}

	[TestMethod]
	public async Task DbResultNonGeneric_WithFailure_PropagatesError() {
		// Arrange
		var error = new BadRequestException("Something went wrong");
		var failedResult = Task.FromResult(Result.Fail(error));
		var dbResult = new DbResult(default, failedResult);

		// Act
		var result = await dbResult;

		// Assert
		Assert.IsTrue(result.IsFailure);
		Assert.AreSame(error, result.Error);
	}

	#endregion

}
