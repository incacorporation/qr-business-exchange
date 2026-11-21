# .NET 10 Package Modernization - Execution Tasks

## Progress Dashboard

**Strategy**: All-At-Once
**Total Tasks**: 10
**Completed**: 0
**In Progress**: 0
**Remaining**: 10

**Progress**: 6/10 tasks complete (60%) ![60%](https://progress-bar.xyz/60)

---

## Task List

### Phase 1: Preparation

#### [?] TASK-001: Prerequisites Validation *(Completed: 2026-01-23 23:17)*
**Description**: Verify environment and starting state before updates

**Actions**:
- [?] (1) Verify .NET 10 SDK installed: `dotnet --version` (should be 10.x)
- [?] (2) Check current branch: `git branch --show-current` (should be `main`)
- [?] (3) Verify clean build: `dotnet build` (should succeed with 0 errors)
- [?] (4) Verify tests pass: `dotnet test` (baseline test results)
- [?] (5) Check for pending changes: `git status` (should be clean or documented)

**Expected Outcome**: Environment ready, baseline established, no blockers identified

**Verification**: All prerequisite checks pass

---

#### [?] TASK-002: Create Upgrade Branch
**Description**: Create dedicated branch for package modernization

**Actions**:
- [?] (1) Ensure on main branch: `git checkout main`
- [?] (2) Pull latest changes: `git pull origin main`
- [?] (3) Create upgrade branch: `git checkout -b upgrade/net10-package-modernization`
- [?] (4) Verify new branch created: `git branch --show-current`

**Expected Outcome**: New branch `upgrade/net10-package-modernization` created and checked out

**Verification**: `git branch --show-current` returns `upgrade/net10-package-modernization`

---

### Phase 2: Package Updates (All-At-Once)

#### [?] TASK-003: Update QRBusinessExchange.Infrastructure Packages *(Completed: 2026-01-23 23:23)*
**Description**: Update all 8 packages in Infrastructure project

**Actions**:
- [?] (1) Open `src\QRBusinessExchange.Infrastructure\QRBusinessExchange.Infrastructure.csproj`
- [?] (2) Update `Microsoft.EntityFrameworkCore.SqlServer` from 8.0.11 to **10.0.2**
- [?] (3) Update `Microsoft.EntityFrameworkCore.Tools` from 8.0.11 to **10.0.2**
- [?] (4) Update `System.IdentityModel.Tokens.Jwt` from 8.0.2 to **8.15.0**
- [?] (5) Update `Newtonsoft.Json` from 13.0.3 to **13.0.4**
- [?] (6) Update `QRCoder` from 1.6.0 to **1.7.0**
- [?] (7) Keep `BCrypt.Net-Next` at 4.0.3 (already latest)
- [?] (8) Keep `RestSharp` at 113.1.0 (skip alpha)
- [?] (9) Keep `Stripe.net` at 50.2.0 (skip alpha)
- [?] (10) Save file

**Expected Outcome**: Infrastructure project has 5 packages updated, 3 packages unchanged

**Verification**: File saved with correct package versions

---

#### [?] TASK-004: Update QRBusinessExchange.API Packages *(Completed: 2026-01-23 23:26)*
**Description**: Update all 7 packages in API project

**Actions**:
- [?] (1) Open `src\QRBusinessExchange.API\QRBusinessExchange.API.csproj`
- [?] (2) Update `Microsoft.AspNetCore.Authentication.JwtBearer` from 8.0.11 to **10.0.2**
- [?] (3) Update `Microsoft.AspNetCore.OpenApi` from 8.0.23 to **10.0.2**
- [?] (4) Update `Microsoft.EntityFrameworkCore.SqlServer` from 8.0.11 to **10.0.2**
- [?] (5) Update `Microsoft.EntityFrameworkCore.Tools` from 8.0.11 to **10.0.2**
- [?] (6) Update `Swashbuckle.AspNetCore` from 7.2.0 to **10.1.0**
- [?] (7) Update `System.IdentityModel.Tokens.Jwt` from 8.2.1 to **8.15.0**
- [?] (8) Keep `BCrypt.Net-Next` at 4.0.3 (already latest)
- [?] (9) Save file

**Expected Outcome**: API project has 6 packages updated, 1 package unchanged

**Verification**: File saved with correct package versions

---

#### [?] TASK-005: Update QRBusinessExchange.UnitTests Packages *(Completed: 2026-01-23 23:28)*
**Description**: Update all 4 test packages in UnitTests project

**Actions**:
- [?] (1) Open `tests\QRBusinessExchange.UnitTests\QRBusinessExchange.UnitTests.csproj`
- [?] (2) Update `Microsoft.NET.Test.Sdk` from 17.8.0 to **18.0.1**
- [?] (3) Update `xunit` from 2.5.3 to **2.9.3**
- [?] (4) Update `xunit.runner.visualstudio` from 2.5.3 to **3.1.5**
- [?] (5) Update `coverlet.collector` from 6.0.0 to **6.0.4**
- [?] (6) Save file

**Expected Outcome**: UnitTests project has all 4 packages updated

**Verification**: File saved with correct package versions

---

#### [?] TASK-006: Update QRBusinessExchange.IntegrationTests Packages *(Completed: 2026-01-23 23:30)*
**Description**: Update all 4 test packages in IntegrationTests project

**Actions**:
- [?] (1) Open `tests\QRBusinessExchange.IntegrationTests\QRBusinessExchange.IntegrationTests.csproj`
- [?] (2) Update `Microsoft.NET.Test.Sdk` from 17.8.0 to **18.0.1**
- [?] (3) Update `xunit` from 2.5.3 to **2.9.3**
- [?] (4) Update `xunit.runner.visualstudio` from 2.5.3 to **3.1.5**
- [?] (5) Update `coverlet.collector` from 6.0.0 to **6.0.4**
- [?] (6) Save file

**Expected Outcome**: IntegrationTests project has all 4 packages updated

**Verification**: File saved with correct package versions

---

### Phase 3: Build & Validation

#### [?] TASK-007: Restore and Build Solution *(Completed: 2026-01-23 23:35)*
**Description**: Restore NuGet packages and build entire solution

**Actions**:
- [?] (1) Navigate to solution root
- [?] (2) Restore packages: `dotnet restore`
- [?] (3) Build solution: `dotnet build --no-restore`
- [?] (4) Review build output for errors
- [?] (5) Review build output for warnings (especially package-related)

**Expected Outcome**: 
- Restore completes successfully
- Build succeeds with 0 errors
- No package conflict warnings
- No package downgrade warnings

**Verification**: `dotnet build` exits with code 0, zero errors

---

#### [?] TASK-008: Run Unit Tests *(Completed: 2026-01-23 23:37)*
**Description**: Execute unit test suite to validate functionality

**Actions**:
- [?] (1) Run unit tests: `dotnet test tests/QRBusinessExchange.UnitTests/QRBusinessExchange.UnitTests.csproj --no-build`
- [?] (2) Review test results
- [?] (3) Verify test discovery worked (all tests found)
- [?] (4) Check for any new test failures

**Expected Outcome**: All unit tests pass, test runner v3.1.5 discovers tests correctly

**Verification**: All tests pass with 100% success rate

---

#### [?] TASK-009: Run Integration Tests *(Completed: 2026-01-23 23:39)*
**Description**: Execute integration test suite to validate system integration

**Actions**:
- [?] (1) Run integration tests: `dotnet test tests/QRBusinessExchange.IntegrationTests/QRBusinessExchange.IntegrationTests.csproj --no-build`
- [?] (2) Review test results
- [?] (3) Verify database connections work (EF Core 10)
- [?] (4) Check for any new test failures

**Expected Outcome**: All integration tests pass, EF Core 10 works with existing database

**Verification**: All tests pass with 100% success rate

---

#### [?] TASK-010: Manual API & Swagger Validation *(Completed: 2026-01-23 23:53)*
**Description**: Manually validate API functionality and Swagger UI

**Actions**:
- [?] (1) Start API: `cd src/QRBusinessExchange.API && dotnet run`
- [?] (2) Navigate to Swagger UI: `https://localhost:<port>/swagger`
- [?] (3) Verify Swagger UI loads without errors
- [?] (4) Check all endpoints are documented
- [?] (5) Test "Authorize" button (JWT Bearer)
- [?] (6) Test "Try it out" on a sample endpoint
- [?] (7) Verify no console errors or exceptions
- [?] (8) Stop API (Ctrl+C)

**Expected Outcome**: 
- API starts successfully
- Swagger UI fully functional (Swashbuckle 10.1.0)
- JWT authentication works
- No runtime errors

**Verification**: Swagger UI functional, API responds correctly

---

### Phase 4: Commit & Finalize

#### [?] TASK-011: Commit Package Updates
**Description**: Commit all package updates to upgrade branch

**Actions**:
- [ ] (1) Review changes: `git status`
- [ ] (2) Review diff: `git diff src/` and `git diff tests/`
- [ ] (3) Stage all .csproj changes: `git add src/ tests/`
- [ ] (4) Commit with message:
```
feat(packages): Modernize NuGet packages for .NET 10

Update all package references to .NET 10 aligned versions across
solution. Includes Microsoft framework packages, third-party libraries,
and test infrastructure.

Key updates:
- ASP.NET Core packages to 10.0.2
- Entity Framework Core to 10.0.2
- Swashbuckle to 10.1.0 (major version)
- xUnit test runner to 3.1.5
- Standardized JWT tokens to 8.15.0

BREAKING CHANGE: None. All updates backward compatible.

Changes:
- QRBusinessExchange.API: 6 packages updated
- QRBusinessExchange.Infrastructure: 5 packages updated
- QRBusinessExchange.UnitTests: 4 packages updated
- QRBusinessExchange.IntegrationTests: 4 packages updated

Validation:
? All projects build successfully
? All tests pass (unit + integration)
? API functional with Swagger UI
? No breaking changes identified
```
- [ ] (5) Verify commit: `git log -1 --stat`

**Expected Outcome**: Single atomic commit with all package updates

**Verification**: Commit created with all 4 .csproj files modified

---

#### [ ] TASK-012: Final Validation & Documentation
**Description**: Perform final checks and update documentation

**Actions**:
- [ ] (1) Verify no vulnerable packages: `dotnet list package --vulnerable`
- [ ] (2) Verify no outdated critical packages: `dotnet list package --outdated`
- [ ] (3) Review execution log for any warnings or notes
- [ ] (4) Update CHANGELOG.md if applicable
- [ ] (5) Update README.md if package requirements changed
- [ ] (6) Push branch: `git push origin upgrade/net10-package-modernization`

**Expected Outcome**: 
- Zero vulnerabilities
- Documentation updated
- Branch pushed to remote

**Verification**: All final checks pass, branch available remotely

---

## Execution Log

*(This section will be updated during task execution)*

---

## Notes

- **Strategy**: All-At-Once (all packages updated simultaneously)
- **Total Packages Updated**: 13 unique packages
- **Projects Modified**: 4 (.csproj files)
- **Expected Duration**: 20-30 minutes
- **Rollback**: Single commit revert if issues arise

---

**Generated**: Based on plan.md
**Plan Reference**: H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange\.github\upgrades\plan.md
**Assessment Reference**: H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange\.github\upgrades\assessment.md
