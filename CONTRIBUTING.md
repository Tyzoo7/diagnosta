# Contributing

Thanks for your interest in Diagnosta!

## Bugs and ideas

Open an [issue](https://github.com/Tyzoo7/diagnosta/issues/new/choose) using one of the
templates. Security problems go through [SECURITY.md](SECURITY.md) instead.

## Pull requests

Small fixes are welcome. For bigger changes, please open an issue first so we can agree on
the idea before you spend time on it.

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Build and test:
   ```powershell
   dotnet restore --locked-mode
   dotnet test
   ```
3. Keep to the [principles](README.md#principles): read-only, no admin rights, no network
   access, no telemetry, as few dependencies as possible.
4. New text appears in both `Strings.resx` (English) and `Strings.de.resx` (German).
5. Every pull request must pass CI before it can be merged.

By contributing, you agree that your work is published under the [MIT license](LICENSE).
