# Contributing to IronLogger

First off, thank you for considering contributing to IronLogger! People like you make the open-source community such an amazing place to learn, inspire, and create.

## 1. Where do I go from here?

If you've noticed a bug or have a question, search the [issue tracker](https://github.com/IronLogger/IronLogger/issues) to see if someone else in the community has already created a ticket. If not, go ahead and make one!

## 2. Setting up your environment

1. Make sure you have the latest **.NET 8 SDK** installed.
2. Fork the repository on GitHub.
3. Clone the forked repository to your local machine:
   ```bash
   git clone https://github.com/YourUsername/IronLogger.git
   ```
4. Build the solution to ensure everything works natively:
   ```bash
   dotnet build
   ```

## 3. Making Changes

1. Create a new branch: `git checkout -b my-feature-branch`.
2. Make your changes in the source code.
3. Keep your coding style consistent with the project (`.editorconfig` takes care of most things automatically). We strongly enforce zero warnings and zero statically determined allocations. Please do not bypass `Directory.Build.props` TreatWarningsAsErrors logic unless absolutely critical.
4. Add unit / integration tests covering your feature. We strive for 80%+ branch coverage.

## 4. Running Tests

Before submitting a Pull Request, verify that all tests pass:

```bash
dotnet test
```

> **Note**: For integration tests spanning `ClickHouseLogger.Tests.Integration`, you must have **Docker** running on your local machine. Testcontainers automatically provisions ClickHouse instances and executes the ADO.NET SQL schema.

## 5. Submitting a Pull Request

- Push your branch to your fork.
- Open a Pull Request against the `main` branch of the IronLogger repository.
- Describe the changes you made, referencing any issues directly.
- The CI pipeline will run automatically and evaluate tests and code coverage. Ensure the run passes.

## 6. Code of Conduct

Please note that this project is released with a Contributor Code of Conduct. By participating in this project you agree to abide by its terms.
