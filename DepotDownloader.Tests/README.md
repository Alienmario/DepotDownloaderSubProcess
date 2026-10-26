# DepotDownloader.Tests

This project contains unit and integration tests for the DepotDownloader library.

## Running Tests

### From Command Line

To run all tests:
```bash
dotnet test
```

To run tests with detailed output:
```bash
dotnet test --logger "console;verbosity=detailed"
```

To run only unauthenticated tests:
```bash
dotnet test --filter "FullyQualifiedName~DepotDownloaderApiTests"
```

### Authenticated Tests

Tests that require Steam account credentials live in `DepotDownloaderAuthenticatedTests` and are automatically skipped if credentials are not configured.

To configure credentials using .NET User Secrets (from the `DepotDownloader.Tests` directory):
```bash
dotnet user-secrets set "username" "..."
dotnet user-secrets set "pw" "..."
```

To run only the authenticated tests:
```bash
dotnet test --filter "FullyQualifiedName~DepotDownloaderAuthenticatedTests"
```

### From JetBrains Rider

1. Open the Test Explorer (View → Tool Windows → Unit Tests)
2. Right-click on the test project or specific tests
3. Select "Run" or "Debug"

## Adding New Tests

1. Create a new test class in the `DepotDownloader.Tests` namespace (or add to pre-existing class)
2. Use the `[Fact]` attribute for test methods
3. Use xUnit assertions (Assert.Equal, Assert.NotNull, etc.)
4. Follow the Arrange-Act-Assert pattern

Example:
```csharp
[Fact]
public void MyTest_ShouldDoSomething()
{
    // Arrange
    var input = "test";

    // Act
    var result = SomeMethod(input);

    // Assert
    Assert.NotNull(result);
}
```

## Test Coverage

The test project uses `InternalsVisibleTo` to access internal members of the DepotDownloader library, allowing for more comprehensive testing of internal functionality.
