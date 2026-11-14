# Versioning & Release Rules

AI PriceWatch follows **Semantic Versioning 2.0.0** (`MAJOR.MINOR.PATCH`) driven by **Conventional Commits** and automated via GitHub Actions.

## Version Format

All assemblies and installers share a single source of truth in [`Directory.Build.props`](../Directory.Build.props):

- **`MAJOR` (`X.0.0`)**: Breaking changes to saved user preferences/config schema, default global hotkeys, or major UI/architecture overhauls.
- **`MINOR` (`0.X.0`)**: Backwards-compatible features (`feat:`), new AI providers, new HUD controls, or major model catalog additions.
- **`PATCH` (`0.0.X`)**: Bug fixes (`fix:`), embedded `pricing-catalog.json` rate corrections (`fix(catalog):`), or minor polish.

Git release tags use the `v` prefix (for example, `v0.1.0`, `v1.0.0`).

## Commit Message Rules (Enforced in CI)

Every commit on a Pull Request to `main` must follow [Conventional Commits](https://www.conventionalcommits.org/):

```text
<type>[optional scope][optional !]: <description>
```

Allowed `<type>` values:

| Type | SemVer Impact | When to use |
|---|---|---|
| `feat` | `MINOR` | New user-facing feature or provider |
| `fix` | `PATCH` | Bug fix or pricing catalog correction (`fix(catalog): ...`) |
| `perf` | `PATCH` | Performance improvement |
| `refactor` | None | Code restructuring with no behavior change |
| `docs` | None | Documentation or README updates |
| `style` | None | Formatting or XAML/code style tweaks |
| `test` | None | Adding or updating tests |
| `build` | None | Build system, `.csproj`, or dependency updates |
| `ci` | None | GitHub Actions or release workflow changes |
| `chore` | None | Maintenance tasks |

To trigger a **`MAJOR`** bump, append `!` after the type/scope (e.g., `feat!: change preferences file format`) or include a `BREAKING CHANGE:` footer in the commit body.

## How Releases Work

1. Merge Conventional Commits into `main`.
2. **CI** (`.github/workflows/ci.yml`) validates commit messages, builds the solution, and runs `AiPriceWatch.Tests`.
3. **Release Please** (`.github/workflows/release-please.yml`) automatically opens/updates a Release PR that bumps `Directory.Build.props`, updates `CHANGELOG.md`, and prepares the next `vX.Y.Z` tag.
4. Merging the Release PR creates the GitHub tag and triggers `.github/workflows/release.yml` to build, sign, and publish the Windows installer.
