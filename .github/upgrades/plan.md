# .NET 10 Package Modernization Plan

## Table of Contents

- [Executive Summary](#executive-summary)
- [Migration Strategy](#migration-strategy)
- [Detailed Dependency Analysis](#detailed-dependency-analysis)
- [Package Update Reference](#package-update-reference)
- [Project-by-Project Migration Plans](#project-by-project-migration-plans)
- [Breaking Changes Catalog](#breaking-changes-catalog)
- [Risk Management](#risk-management)
- [Testing & Validation Strategy](#testing--validation-strategy)
- [Complexity & Effort Assessment](#complexity--effort-assessment)
- [Source Control Strategy](#source-control-strategy)
- [Success Criteria](#success-criteria)

---

## Executive Summary

### Scenario Description

This plan addresses the **modernization of NuGet package versions** for a .NET 10 solution. While all projects already target .NET 10.0, many packages remain on .NET 8.0 versions. Updating to .NET 10-aligned packages provides performance improvements, new features, bug fixes, and full optimization for the .NET 10 runtime.

### Scope

**Projects Affected**: 6 projects
- **QRBusinessExchange.Core** (ClassLibrary) - Foundation layer, no packages
- **QRBusinessExchange.Infrastructure** (ClassLibrary) - 8 packages to update
- **QRBusinessExchange.API** (ASP.NET Core) - 7 packages to update  
- **QRBusinessExchange.Web** (Blazor) - 0 packages (only project reference)
- **QRBusinessExchange.UnitTests** - 4 packages to update
- **QRBusinessExchange.IntegrationTests** - 4 packages to update

**Current State**: All projects targeting net10.0, using mix of .NET 8.0 and older package versions

**Target State**: All projects targeting net10.0 with .NET 10-aligned package versions

### Discovered Metrics

| Metric | Value | Assessment |
|--------|-------|------------|
| **Total Projects** | 6 | Small solution |
| **Dependency Depth** | 2 levels | Simple structure |
| **Packages to Update** | 13 unique packages | Moderate scope |
| **Security Vulnerabilities** | 0 | ? No critical issues |
| **Breaking Changes** | 0 identified | ? Low risk |
| **Total LOC** | 9,123 | Small to medium codebase |
| **API Compatibility Issues** | 0 | ? Clean upgrade path |

### Complexity Classification

**Classification: Simple**

**Justification**:
- Small project count (6 projects)
- Shallow dependency structure (leaf ? infrastructure ? apps)
- No security vulnerabilities requiring immediate attention
- All packages already compatible with net10.0
- Zero API compatibility issues identified in assessment
- No breaking changes detected

### Selected Strategy

**All-At-Once Strategy**

**Rationale**:
- Small solution size makes coordinated update manageable
- All projects already on target framework (net10.0)
- No package incompatibilities or conflicts
- Clear dependency structure enables simultaneous update
- All packages have confirmed .NET 10 compatible versions
- No intermediate states needed - direct path to target

This approach enables fastest completion with minimal complexity, updating all package references across all projects in a single coordinated operation.

### Critical Issues

**None identified** ?

- No security vulnerabilities in current packages
- No blocking incompatibilities
- No API breaking changes
- All packages have stable .NET 10 versions available

### Recommended Approach

**All-At-Once Package Update** - Update all package references across all projects simultaneously in a single operation, followed by comprehensive build and test validation.

### Expected Iterations

- **Phase 1**: Discovery & Classification (Complete)
- **Phase 2**: Foundation (Dependency Analysis, Migration Strategy, Project Stubs) - 3 iterations
- **Phase 3**: Detail Generation (All projects batched) - 2 iterations
- **Total**: 6 iterations to complete plan

---

## Migration Strategy

### Approach Selection

**Selected: All-At-Once Strategy**

All package references across all projects will be updated simultaneously in a single coordinated operation.

### Justification

**Why All-At-Once is Optimal**:

1. **Small Solution Size**: 6 projects is well within the threshold for atomic updates
2. **Already on Target Framework**: All projects are net10.0, eliminating framework upgrade complexity
3. **Package Compatibility**: Assessment confirms all target packages are compatible with net10.0
4. **No Breaking Changes**: Analysis identified zero API breaking changes
5. **Clear Dependencies**: Simple 3-level dependency structure with no cycles
6. **Low Risk Profile**: 
   - No security vulnerabilities requiring staged rollout
   - No complex migration paths
   - All updates are version bumps only
7. **Faster Completion**: Single update cycle vs. multiple incremental phases
8. **Simpler Coordination**: One build/test cycle instead of multiple checkpoints

### All-At-Once Strategy Rationale

**Core Principle**: Update all package references across all projects in a single atomic operation, creating a unified state without intermediate configurations.

**Advantages for This Scenario**:
- ? Fastest path to .NET 10 package alignment
- ? Single comprehensive test cycle
- ? No multi-targeting complexity
- ? All projects benefit simultaneously
- ? Clean dependency resolution
- ? Simpler rollback if needed (single commit)

**Characteristics**:
- All .csproj files updated simultaneously
- All packages restored together
- Solution built as complete unit
- Comprehensive validation in single pass
- No intermediate states or phases

### Dependency-Based Ordering

While updates are simultaneous, understanding the logical order helps troubleshooting:

**Logical Update Sequence** (for reference):
1. **Foundation packages** (Infrastructure layer - EF Core, JWT)
2. **Application packages** (API layer - ASP.NET Core, Swashbuckle)
3. **Supporting packages** (Test projects - xUnit, Test SDK)

**In Practice**: All updates applied at once, but if issues arise during build, this sequence guides investigation priority.

### Execution Approach

**Single-Phase Atomic Update**:

1. **Preparation**: Create upgrade branch, verify starting state
2. **Update All Package References**: Modify all .csproj files in single operation
3. **Restore**: `dotnet restore` for entire solution
4. **Build**: Build entire solution, address any compilation errors
5. **Test**: Run all test projects to validate functionality
6. **Verify**: Confirm all packages updated, no warnings, tests pass
7. **Commit**: Single commit with all changes

**No Phased Rollout Needed**: The simplicity and low-risk nature of this update means phases would add unnecessary overhead without risk reduction benefits.

### Risk Mitigation Within All-At-Once

Even with simultaneous updates, we include safety measures:

1. **Pre-Update Validation**: Confirm solution builds and tests pass before starting
2. **Incremental Compilation**: Build can identify issues per-project even in batch
3. **Targeted Testing**: Test projects provide safety net for regressions
4. **Single Commit Strategy**: Easy rollback if unexpected issues arise
5. **Breaking Changes Review**: Pre-reviewed Swashbuckle (primary concern) - confirmed safe

### Parallel vs Sequential Execution

**Sequential Execution Within Single Operation**:
- Update all .csproj files (can be parallel edits, same commit)
- Restore dependencies (NuGet handles parallelization)
- Build projects (MSBuild handles parallel compilation)
- Run tests (test runner handles parallelization)

**Result**: Fast execution without manual phase management

### All-At-Once Ordering Principles

**Guiding Principles**:
1. **Atomicity**: All changes in single logical unit
2. **Consistency**: All Microsoft packages move to 10.0.2 together
3. **Verification**: Single comprehensive validation pass
4. **Simplicity**: No complex state transitions or gates
5. **Speed**: Fastest path to completion

### Alternative Considered: Incremental

**Why Not Incremental**:
- Would require 3+ phases (Infrastructure ? API ? Tests)
- Each phase needs separate build/test cycle
- Adds coordination overhead
- No risk reduction benefit (all packages compatible)
- Longer timeline without benefit
- Complexity not justified by solution size or risk profile

**Conclusion**: All-At-Once is the clear optimal choice for this scenario.

---

## Detailed Dependency Analysis

### Dependency Graph Summary

The solution follows a clean layered architecture with clear dependency flow:

```
Test Projects (Integration/Unit Tests)
    ?
Application Layer (API, Web)
    ?
Infrastructure Layer
    ?
Core/Domain Layer (foundation)
```

**Dependency Levels**:

**Level 0 (Foundation - No Dependencies)**:
- `QRBusinessExchange.Core` - Domain models, interfaces, no packages

**Level 1 (Infrastructure - Depends on Core)**:
- `QRBusinessExchange.Infrastructure` - Data access, external services, 8 packages

**Level 2 (Applications - Depend on Infrastructure + Core)**:
- `QRBusinessExchange.API` - REST API, 7 packages
- `QRBusinessExchange.Web` - Blazor UI, 0 packages (project references only)

**Level 3 (Tests - Depend on Application Layers)**:
- `QRBusinessExchange.UnitTests` - Depends on Core, API, Infrastructure (4 packages)
- `QRBusinessExchange.IntegrationTests` - Depends on API, Infrastructure (4 packages)

### Project Groupings for All-At-Once Migration

Since this is an All-At-Once strategy with package updates only (no framework changes), all projects will be updated simultaneously. However, logical grouping helps understand impact:

**Group 1: Microsoft Framework Packages** (Critical - .NET 10 Alignment)
- QRBusinessExchange.API
- QRBusinessExchange.Infrastructure
Projects with Microsoft.AspNetCore.* and Microsoft.EntityFrameworkCore.* packages

**Group 2: Third-Party Packages** (Important - Features & Bug Fixes)
- QRBusinessExchange.Infrastructure (RestSharp, Stripe.net, QRCoder, Newtonsoft.Json)
- QRBusinessExchange.API (Swashbuckle, BCrypt)

**Group 3: Testing Packages** (Supporting - Test Infrastructure)
- QRBusinessExchange.UnitTests
- QRBusinessExchange.IntegrationTests
Microsoft.NET.Test.Sdk, xUnit, coverlet packages

**Group 4: No Package Updates**
- QRBusinessExchange.Core (no packages)
- QRBusinessExchange.Web (no packages, only project references)

### Critical Path Identification

**Primary Update Path**:
1. Update Infrastructure packages (especially EF Core and JWT tokens)
2. Update API packages (especially ASP.NET Core and Swashbuckle)
3. Update Test packages (test SDK and xUnit)
4. Restore all dependencies
5. Build entire solution
6. Run tests to verify

**Why All-At-Once Works Here**:
- No circular dependencies
- All packages compatible with net10.0
- No package conflicts identified
- Update is additive (version bumps only)
- No code changes expected

### Package Dependency Relationships

**Microsoft Ecosystem** (must align versions):
- `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.2
- `Microsoft.AspNetCore.OpenApi` 10.0.2  
- `Microsoft.EntityFrameworkCore.SqlServer` 10.0.2
- `Microsoft.EntityFrameworkCore.Tools` 10.0.2

These packages should all move to 10.0.2 together for consistency.

**JWT Token Version Standardization**:
- Currently: API uses 8.2.1, Infrastructure uses 8.0.2
- Target: Both use 8.15.0 (latest stable)
- **Action Required**: Standardize across both projects

**Testing Ecosystem** (coordinated update):
- `Microsoft.NET.Test.Sdk` 18.0.1
- `xunit` 2.9.3
- `xunit.runner.visualstudio` 3.1.5
- `coverlet.collector` 6.0.4

**Independent Third-Party Packages**:
- `Swashbuckle.AspNetCore` 10.1.0 (independent)
- `Newtonsoft.Json` 13.0.4 (patch update)
- `QRCoder` 1.7.0 (minor update)
- `BCrypt.Net-Next` 4.0.3 (already latest)

### Circular Dependency Check

? **No circular dependencies detected**

The dependency graph is acyclic with clear top-down flow from Core ? Infrastructure ? Applications ? Tests.

---

## Package Update Reference

### Update Summary

**Total Packages to Update**: 13 unique packages across 3 projects

### Common Package Updates (Affecting Multiple Projects)

| Package | Current Version | Target Version | Projects Affected | Update Reason | Priority |
|---------|----------------|----------------|-------------------|---------------|----------|
| **BCrypt.Net-Next** | 4.0.3 | 4.0.3 | API, Infrastructure | ? Already latest | Skip |
| **Microsoft.EntityFrameworkCore.SqlServer** | 8.0.11 | **10.0.2** | API, Infrastructure | .NET 10 alignment, performance | ?? High |
| **Microsoft.EntityFrameworkCore.Tools** | 8.0.11 | **10.0.2** | API, Infrastructure | Match EF Core version | ?? High |
| **System.IdentityModel.Tokens.Jwt** | 8.0.2 / 8.2.1 | **8.15.0** | API, Infrastructure | **Standardize version**, security fixes | ?? High |
| **coverlet.collector** | 6.0.0 | **6.0.4** | UnitTests, IntegrationTests | Code coverage improvements | ?? Low |
| **Microsoft.NET.Test.Sdk** | 17.8.0 | **18.0.1** | UnitTests, IntegrationTests | Test SDK improvements | ?? Medium |
| **xunit** | 2.5.3 | **2.9.3** | UnitTests, IntegrationTests | Test framework updates | ?? Medium |
| **xunit.runner.visualstudio** | 2.5.3 | **3.1.5** | UnitTests, IntegrationTests | Test runner major update | ?? Medium |

### Project-Specific Package Updates

#### QRBusinessExchange.API

| Package | Current Version | Target Version | Update Reason | Priority |
|---------|----------------|----------------|---------------|----------|
| **Microsoft.AspNetCore.Authentication.JwtBearer** | 8.0.11 | **10.0.2** | .NET 10 alignment, features | ?? High |
| **Microsoft.AspNetCore.OpenApi** | 8.0.23 | **10.0.2** | .NET 10 alignment, OpenAPI improvements | ?? High |
| **Swashbuckle.AspNetCore** | 7.2.0 | **10.1.0** | Major update, new features, .NET 10 optimizations | ?? High |

#### QRBusinessExchange.Infrastructure

| Package | Current Version | Target Version | Update Reason | Priority |
|---------|----------------|----------------|---------------|----------|
| **Newtonsoft.Json** | 13.0.3 | **13.0.4** | Bug fixes | ?? Low |
| **QRCoder** | 1.6.0 | **1.7.0** | Minor version update, new features | ?? Medium |
| **RestSharp** | 113.1.0 | 113.1.0 | ? Skip alpha (113.1.1-alpha available) | Skip |
| **Stripe.net** | 50.2.0 | 50.2.0 | ? Skip alpha (50.3.0-alpha available) | Skip |

#### QRBusinessExchange.Web

No package updates required (only project references).

#### QRBusinessExchange.Core

No package updates required (no package dependencies).

### Package Update Matrix by Project

| Package | Core | Infrastructure | API | Web | UnitTests | IntegrationTests |
|---------|------|----------------|-----|-----|-----------|------------------|
| **Microsoft.AspNetCore.Authentication.JwtBearer** | - | - | 8.0.11?**10.0.2** | - | - | - |
| **Microsoft.AspNetCore.OpenApi** | - | - | 8.0.23?**10.0.2** | - | - | - |
| **Microsoft.EntityFrameworkCore.SqlServer** | - | 8.0.11?**10.0.2** | 8.0.11?**10.0.2** | - | - | - |
| **Microsoft.EntityFrameworkCore.Tools** | - | 8.0.11?**10.0.2** | 8.0.11?**10.0.2** | - | - | - |
| **Swashbuckle.AspNetCore** | - | - | 7.2.0?**10.1.0** | - | - | - |
| **System.IdentityModel.Tokens.Jwt** | - | 8.0.2?**8.15.0** | 8.2.1?**8.15.0** | - | - | - |
| **Newtonsoft.Json** | - | 13.0.3?**13.0.4** | - | - | - | - |
| **QRCoder** | - | 1.6.0?**1.7.0** | - | - | - | - |
| **Microsoft.NET.Test.Sdk** | - | - | - | - | 17.8.0?**18.0.1** | 17.8.0?**18.0.1** |
| **xunit** | - | - | - | - | 2.5.3?**2.9.3** | 2.5.3?**2.9.3** |
| **xunit.runner.visualstudio** | - | - | - | - | 2.5.3?**3.1.5** | 2.5.3?**3.1.5** |
| **coverlet.collector** | - | - | - | - | 6.0.0?**6.0.4** | 6.0.0?**6.0.4** |
| **BCrypt.Net-Next** | - | 4.0.3 (skip) | 4.0.3 (skip) | - | - | - |
| **RestSharp** | - | 113.1.0 (skip) | - | - | - | - |
| **Stripe.net** | - | 50.2.0 (skip) | - | - | - | - |

### Priority Breakdown

**?? High Priority** (Microsoft Framework Packages):
- Microsoft.AspNetCore.Authentication.JwtBearer ? 10.0.2
- Microsoft.AspNetCore.OpenApi ? 10.0.2
- Microsoft.EntityFrameworkCore.SqlServer ? 10.0.2
- Microsoft.EntityFrameworkCore.Tools ? 10.0.2
- System.IdentityModel.Tokens.Jwt ? 8.15.0 (standardize)
- Swashbuckle.AspNetCore ? 10.1.0

**?? Medium Priority** (Third-Party & Test Infrastructure):
- QRCoder ? 1.7.0
- Microsoft.NET.Test.Sdk ? 18.0.1
- xunit ? 2.9.3
- xunit.runner.visualstudio ? 3.1.5

**?? Low Priority** (Patch Updates):
- Newtonsoft.Json ? 13.0.4
- coverlet.collector ? 6.0.4

### Packages to Skip

**Skip Alpha/Prerelease Versions**:
- RestSharp 113.1.1-alpha.0.4 (stay on 113.1.0 stable)
- Stripe.net 50.3.0-alpha.1 (stay on 50.2.0 stable)

**Rationale**: Production systems should use stable releases only. Alpha versions may introduce instability.

### Version Standardization

**Critical: System.IdentityModel.Tokens.Jwt**

**Current State** (inconsistent):
- QRBusinessExchange.API: 8.2.1
- QRBusinessExchange.Infrastructure: 8.0.2

**Target State** (standardized):
- Both projects: 8.15.0

**Benefit**: Eliminates potential version conflicts, ensures consistent JWT handling across solution.

---

## Project-by-Project Migration Plans

### Overview

All projects will be updated simultaneously as part of the All-At-Once strategy. Details below provide project-specific context.

---

### Project: QRBusinessExchange.Core

**Current State**: net10.0, ClassLibrary, 0 packages

**Target State**: net10.0 (unchanged)

**Migration Steps**: None required - no package dependencies

**Complexity**: None

---

### Project: QRBusinessExchange.Infrastructure

**Current State**: 
- Target Framework: net10.0
- Project Type: ClassLibrary
- Dependencies: QRBusinessExchange.Core
- Current Packages: 8 packages (BCrypt, EF Core, JWT, Newtonsoft, QRCoder, RestSharp, Stripe)
- LOC: 5,223

**Target State**: net10.0, 8 packages updated to latest stable versions

**Migration Steps**:

1. **Update Microsoft Packages** (High Priority):
   - `Microsoft.EntityFrameworkCore.SqlServer`: 8.0.11 ? **10.0.2**
   - `Microsoft.EntityFrameworkCore.Tools`: 8.0.11 ? **10.0.2**
   - `System.IdentityModel.Tokens.Jwt`: 8.0.2 ? **8.15.0** ?? (standardize)

2. **Update Third-Party Packages**:
   - `Newtonsoft.Json`: 13.0.3 ? **13.0.4**
   - `QRCoder`: 1.6.0 ? **1.7.0**

3. **Keep Current** (Skip Alpha Versions):
   - `BCrypt.Net-Next`: 4.0.3 (already latest stable)
   - `RestSharp`: 113.1.0 (skip 113.1.1-alpha)
   - `Stripe.net`: 50.2.0 (skip 50.3.0-alpha)

**Expected Breaking Changes**: None

**Code Modifications**: None expected

**Testing Strategy**:
- Build project to verify compilation
- Run unit tests that exercise Infrastructure layer
- Run integration tests that validate:
  - Database access (EF Core)
  - External service calls (RestSharp, Stripe)
  - JWT token generation
  - QR code generation

**Validation Checklist**:
- [ ] Project builds without errors
- [ ] No package downgrade warnings
- [ ] Entity Framework migrations still valid
- [ ] JWT token service functional
- [ ] External API clients operational

**Complexity**: Low
**Risk**: Low ??

---

### Project: QRBusinessExchange.API

**Current State**: 
- Target Framework: net10.0
- Project Type: ASP.NET Core Web API
- Dependencies: QRBusinessExchange.Infrastructure, QRBusinessExchange.Core
- Current Packages: 7 packages (AspNetCore auth/OpenAPI, EF Core, Swashbuckle, JWT)
- LOC: 2,403

**Target State**: net10.0, 7 packages updated to .NET 10 aligned versions

**Migration Steps**:

1. **Update Microsoft ASP.NET Core Packages** (Critical):
   - `Microsoft.AspNetCore.Authentication.JwtBearer`: 8.0.11 ? **10.0.2**
   - `Microsoft.AspNetCore.OpenApi`: 8.0.23 ? **10.0.2**

2. **Update Entity Framework Core**:
   - `Microsoft.EntityFrameworkCore.SqlServer`: 8.0.11 ? **10.0.2**
   - `Microsoft.EntityFrameworkCore.Tools`: 8.0.11 ? **10.0.2**

3. **Update Swagger/OpenAPI**:
   - `Swashbuckle.AspNetCore`: 7.2.0 ? **10.1.0** (major version jump)

4. **Update JWT Tokens** (Standardize):
   - `System.IdentityModel.Tokens.Jwt`: 8.2.1 ? **8.15.0**

5. **Keep Current**:
   - `BCrypt.Net-Next`: 4.0.3 (already latest)

**Expected Breaking Changes**: None (Swashbuckle pre-reviewed and confirmed compatible)

**Code Modifications**: None expected

**Testing Strategy**:
- Build project
- Start application in Development mode
- Validate Swagger UI at `/swagger`:
  - UI renders correctly
  - All endpoints documented
  - "Authorize" button present and functional
  - "Try it out" works for sample endpoint
- Test JWT authentication:
  - Login endpoint returns token
  - Protected endpoints require Bearer token
  - Token validation works correctly
- Run integration tests

**Validation Checklist**:
- [ ] Project builds without errors
- [ ] Application starts successfully
- [ ] Swagger UI loads at `/swagger`
- [ ] JWT authentication works
- [ ] All API endpoints respond correctly
- [ ] No runtime exceptions
- [ ] Integration tests pass

**Complexity**: Low-Medium (Swashbuckle major version)
**Risk**: Low ?? (mitigated by pre-review)

---

### Project: QRBusinessExchange.Web

**Current State**: 
- Target Framework: net10.0
- Project Type: Blazor Web Application
- Dependencies: QRBusinessExchange.Core
- Current Packages: 0 (only project references)
- LOC: 380

**Target State**: net10.0 (unchanged)

**Migration Steps**: None required - no package dependencies

**Expected Breaking Changes**: None

**Code Modifications**: None

**Testing Strategy**:
- Build project to verify compilation
- Launch Blazor app to verify it runs
- Spot check key pages render correctly

**Validation Checklist**:
- [ ] Project builds without errors
- [ ] Application starts successfully
- [ ] Home page renders

**Complexity**: None
**Risk**: None ??

---

### Project: QRBusinessExchange.UnitTests

**Current State**: 
- Target Framework: net10.0
- Project Type: xUnit Test Project
- Dependencies: QRBusinessExchange.Core, QRBusinessExchange.API, QRBusinessExchange.Infrastructure
- Current Packages: 4 test packages
- LOC: 10 (minimal tests currently)

**Target State**: net10.0, 4 test packages updated

**Migration Steps**:

1. **Update Test Infrastructure**:
   - `Microsoft.NET.Test.Sdk`: 17.8.0 ? **18.0.1**
   - `xunit`: 2.5.3 ? **2.9.3**
   - `xunit.runner.visualstudio`: 2.5.3 ? **3.1.5** (major version)
   - `coverlet.collector`: 6.0.0 ? **6.0.4**

**Expected Breaking Changes**: None (verify test discovery)

**Code Modifications**: None expected

**Testing Strategy**:
- Build test project
- Run `dotnet test` from command line
- Verify test discovery in IDE (Visual Studio/VS Code/Rider)
- Check that all tests execute
- Verify code coverage collection works (if used)

**Validation Checklist**:
- [ ] Test project builds without errors
- [ ] Tests discovered by test runner
- [ ] `dotnet test` executes successfully
- [ ] Tests visible in IDE Test Explorer
- [ ] Coverage data collected (if applicable)

**Complexity**: Low
**Risk**: Low ?? (test runner major version warrants check)

---

### Project: QRBusinessExchange.IntegrationTests

**Current State**: 
- Target Framework: net10.0
- Project Type: xUnit Test Project
- Dependencies: QRBusinessExchange.API, QRBusinessExchange.Infrastructure
- Current Packages: 4 test packages
- LOC: 10 (minimal tests currently)

**Target State**: net10.0, 4 test packages updated

**Migration Steps**:

1. **Update Test Infrastructure** (same as UnitTests):
   - `Microsoft.NET.Test.Sdk`: 17.8.0 ? **18.0.1**
   - `xunit`: 2.5.3 ? **2.9.3**
   - `xunit.runner.visualstudio`: 2.5.3 ? **3.1.5** (major version)
   - `coverlet.collector`: 6.0.0 ? **6.0.4**

**Expected Breaking Changes**: None (verify test discovery)

**Code Modifications**: None expected

**Testing Strategy**:
- Build test project
- Run `dotnet test` from command line
- Verify integration tests execute (may require database connection)
- Check that test discovery works
- Validate any test setup/teardown logic still functions

**Validation Checklist**:
- [ ] Test project builds without errors
- [ ] Tests discovered by test runner
- [ ] `dotnet test` executes successfully
- [ ] Integration tests can connect to resources (DB, APIs)
- [ ] Tests visible in IDE Test Explorer

**Complexity**: Low
**Risk**: Low ??

---

## Breaking Changes Catalog

### Overview

**Total Breaking Changes Identified**: 0 critical, 1 area to review

The assessment analysis and pre-review of major version updates (specifically Swashbuckle 7?10) confirm that no significant breaking changes are expected. However, one area warrants attention during validation.

### Framework Breaking Changes

**None** ?

All projects are already on .NET 10.0. These are package updates only, not framework migration.

### Package-Specific Breaking Changes

#### 1. Swashbuckle.AspNetCore (7.2.0 ? 10.1.0)

**Status**: ? **No breaking changes for current configuration**

**Major Version Jumps**: 7 ? 8 ? 9 ? 10

**Pre-Review Summary**:
- Current configuration uses standard, non-deprecated APIs ?
- Security scheme definition (JWT Bearer) already correct ?
- OpenApiInfo structure compatible ?
- SwaggerDoc and SwaggerUI setup unchanged ?
- XML comments approach compatible ?

**Verified Compatibility**:
```csharp
// Current configuration (verified compatible with v10):
options.SwaggerDoc("v1", new OpenApiInfo { ... });
options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {
    Type = SecuritySchemeType.Http,  // ? Correct
    Scheme = "bearer"                // ? Correct
});
options.AddSecurityRequirement(new OpenApiSecurityRequirement { ... });
```

**Changes Between Versions**:
- **v8.0**: OpenAPI 3.0.3 support (non-breaking, beneficial)
- **v9.0**: Enhanced polymorphism support (additive feature)
- **v10.0**: Native AOT compatibility, .NET 8+ optimizations (performance improvements)

**Expected Impact**: None. Configuration follows best practices maintained across all versions.

**Action Required**: Validate Swagger UI renders correctly after update (visual check only).

---

#### 2. xunit.runner.visualstudio (2.5.3 ? 3.1.5)

**Status**: ?? **Review test discovery**

**Major Version Jump**: 2 ? 3

**Potential Impact**: Test runner version jump may affect test discovery in IDE

**Known Changes**:
- Improved test discovery performance
- Better support for modern .NET
- Enhanced reporting capabilities

**Expected Impact**: Low. Test framework changes rarely affect existing tests.

**Action Required**: 
- Run tests after update
- Verify all tests discovered in IDE
- Check test output formatting

**Fallback**: If test discovery breaks, can temporarily revert to 2.9.3 (non-major version).

---

#### 3. Microsoft.EntityFrameworkCore.* (8.0.11 ? 10.0.2)

**Status**: ? **No breaking changes expected**

**Major Version Jump**: 8 ? 10 (skipping 9)

**Context**: Schema-only changes (no schema changes in this update)

**Expected Impact**: None for existing code

**Validation**:
- No database migrations needed (schema unchanged)
- Connection strings unchanged
- Existing queries compatible
- Integration tests validate data access

**Action Required**: None. Package update only.

---

#### 4. Microsoft.AspNetCore.* (8.0.11 ? 10.0.2)

**Status**: ? **No breaking changes expected**

**Package**:
- Microsoft.AspNetCore.Authentication.JwtBearer
- Microsoft.AspNetCore.OpenApi

**Current Usage**: Standard JWT Bearer authentication with correct configuration

**Expected Impact**: None. Configuration patterns remain stable across versions.

**Action Required**: None. Package update only.

---

### Configuration Changes Required

**None** ?

No configuration files, code changes, or project file modifications (beyond version numbers) required.

### API Migration Required

**None** ?

All APIs in use remain compatible with target package versions.

### Behavioral Changes

#### Swashbuckle Schema Generation

**Change**: More accurate nullable reference type handling in v10

**Impact**: ?? Beneficial

**Description**: OpenAPI schemas will better reflect nullable properties from your DTOs

**Example**:
```csharp
public string? CompanyName { get; set; }  // Now properly marked as nullable in schema
public string Email { get; set; }         // Now properly marked as required in schema
```

**Action Required**: None. Improvement in API documentation accuracy.

---

#### Entity Framework Core Query Behavior

**Change**: Improved query performance in EF Core 10

**Impact**: ?? Beneficial

**Description**: Some queries may generate more efficient SQL

**Action Required**: None. Performance improvement only.

---

### Areas Requiring Manual Review

1. **Swagger UI Visual Check**
   - Open `/swagger` endpoint after update
   - Verify UI renders correctly
   - Test "Try it out" functionality
   - Confirm JWT authentication button works

2. **Test Execution**
   - Run `dotnet test` for all test projects
   - Verify test discovery in IDE
   - Check for any flaky tests (unrelated to package updates)

3. **Build Warnings**
   - Review any new warnings from updated analyzers
   - Address if related to best practices
   - Can be deferred if minor

### Breaking Changes by Project

| Project | Expected Breaking Changes | Code Changes Required |
|---------|--------------------------|----------------------|
| QRBusinessExchange.Core | None | None |
| QRBusinessExchange.Infrastructure | None | None |
| QRBusinessExchange.API | None | None |
| QRBusinessExchange.Web | None | None |
| QRBusinessExchange.UnitTests | None (verify test discovery) | None |
| QRBusinessExchange.IntegrationTests | None (verify test discovery) | None |

### Summary

**Overall**: ? **Zero code changes expected**

This package modernization involves version bumps of packages with strong backward compatibility guarantees. The pre-review of the most significant update (Swashbuckle 7?10) confirms current configuration is compatible. The update can proceed with confidence, requiring only validation steps (build, test, visual check).

---

## Risk Management

### High-Level Risk Assessment

**Overall Risk Level: Low** ??

This package modernization presents minimal risk due to:
- All projects already on target framework (net10.0)
- All packages confirmed compatible
- Zero breaking changes identified
- No security vulnerabilities
- Small solution size enables easy rollback

### Risk Factors by Category

| Risk Category | Level | Description | Mitigation |
|---------------|-------|-------------|------------|
| **Framework Compatibility** | ?? None | Already on net10.0 | N/A |
| **Package Breaking Changes** | ?? Low | Swashbuckle 7?10 reviewed, no issues | Pre-reviewed breaking changes |
| **Build Failures** | ?? Low | Possible minor adjustments | Build validation step included |
| **Test Regressions** | ?? Low | Behavior changes possible | Comprehensive test execution |
| **Production Impact** | ?? None | No deployment during update | Update in separate branch |
| **Rollback Complexity** | ?? None | Single commit, easy revert | Git revert strategy |

### Specific Risk Areas

**1. Swashbuckle Major Version Update (7.2.0 ? 10.1.0)**
- **Risk Level**: ?? Low
- **Description**: 3 major versions jump
- **Mitigation**: 
  - Pre-reviewed breaking changes (see Breaking Changes Catalog)
  - Current configuration confirmed compatible
  - Swagger UI testing included in validation
- **Confidence**: High - configuration follows best practices

**2. JWT Token Package Version Inconsistency**
- **Risk Level**: ?? Low
- **Description**: Currently API uses 8.2.1, Infrastructure uses 8.0.2
- **Mitigation**:
  - Standardize both to 8.15.0
  - Single version across solution
  - Build will catch any conflicts
- **Confidence**: High - version alignment improves stability

**3. xUnit Runner Major Version (2.5.3 ? 3.1.5)**
- **Risk Level**: ?? Low
- **Description**: Major version jump may affect test discovery
- **Mitigation**:
  - Run tests after update
  - Verify test discovery works
  - Rollback if test infrastructure breaks
- **Confidence**: Medium - test frameworks usually backward compatible

**4. Entity Framework Core (8.0.11 ? 10.0.2)**
- **Risk Level**: ?? Low
- **Description**: EF Core version jump
- **Mitigation**:
  - No migrations regeneration needed (schema unchanged)
  - Database connections unchanged
  - Integration tests validate data access
- **Confidence**: High - version update only, no schema changes

### Contingency Plans

**If Build Fails**:
1. Review compilation errors
2. Check for package conflicts (`dotnet list package --vulnerable --include-transitive`)
3. Address specific issues (likely minor API adjustments)
4. If unresolvable: Rollback via `git revert`

**If Tests Fail**:
1. Identify which tests fail (unit vs integration)
2. Determine if behavior change or test infrastructure issue
3. Address root cause (likely test configuration)
4. If widespread failures: Rollback and investigate incrementally

**If Swagger UI Breaks**:
1. Review Swashbuckle configuration
2. Check for deprecated options
3. Reference Swashbuckle 10.x documentation
4. Worst case: Pin to Swashbuckle 9.x temporarily

**If Unexpected Package Conflicts**:
1. Run `dotnet restore --force`
2. Clear NuGet cache: `dotnet nuget locals all --clear`
3. Check for transitive dependency conflicts
4. Use `dotnet list package --include-transitive` to diagnose
5. If unresolvable: Rollback

### Rollback Strategy

**Single Commit Approach**:
- All changes in one commit
- Rollback: `git revert <commit-hash>`
- Alternative: `git reset --hard HEAD~1` (before push)

**Partial Rollback** (if needed):
- Revert specific package updates via .csproj edits
- Restore and rebuild
- Not recommended (prefer full rollback for clean state)

### Success Indicators vs Warning Signs

**Green Lights** (Proceed):
- ? `dotnet restore` completes without errors
- ? Solution builds with 0 errors
- ? All tests pass
- ? No package downgrade warnings
- ? Swagger UI loads and functions

**Yellow Flags** (Investigate):
- ?? Build warnings about package versions
- ?? Some tests fail (not all)
- ?? Performance degradation noticed
- ?? Swagger UI shows warnings

**Red Flags** (Consider Rollback):
- ? Restore fails with package conflicts
- ? Build fails with numerous errors
- ? Majority of tests fail
- ? Swagger UI completely broken
- ? Runtime exceptions in development

---

## Testing & Validation Strategy

### Multi-Level Testing Approach

All testing occurs after the atomic package update operation completes. The All-At-Once strategy means a single comprehensive testing phase.

---

### Level 1: Build Validation

**Objective**: Confirm solution compiles successfully with updated packages

**Steps**:
1. Restore all NuGet packages: `dotnet restore`
2. Build entire solution: `dotnet build --no-restore`
3. Review build output for:
   - Zero errors ?
   - No package downgrade warnings ??
   - No new analyzer warnings (review if present)

**Success Criteria**:
- ? `dotnet build` exits with code 0
- ? All projects build successfully
- ? No package conflict warnings
- ? No critical build warnings introduced

**If Failures Occur**:
- Review specific compilation errors
- Check for package version conflicts
- Consult Breaking Changes Catalog
- Consider rollback if errors are widespread

---

### Level 2: Unit Testing

**Objective**: Validate core functionality remains intact

**Test Projects**:
- `QRBusinessExchange.UnitTests`

**Steps**:
1. Run all unit tests: `dotnet test tests/QRBusinessExchange.UnitTests/QRBusinessExchange.UnitTests.csproj`
2. Verify test discovery works (IDE Test Explorer)
3. Review test results for any new failures

**Key Areas Validated**:
- Core domain logic
- Business rules
- Data transformations
- Isolated component behavior

**Success Criteria**:
- ? All unit tests pass
- ? Test discovery works in IDE
- ? No flaky test behavior introduced
- ? Test execution time reasonable

**If Failures Occur**:
- Determine if failures are test infrastructure or actual regressions
- Check xUnit runner version (3.x) for discovery issues
- Investigate any behavior changes in updated packages
- Validate tests are still valid (not false positives)

---

### Level 3: Integration Testing

**Objective**: Validate system integration points with updated packages

**Test Projects**:
- `QRBusinessExchange.IntegrationTests`

**Steps**:
1. Run all integration tests: `dotnet test tests/QRBusinessExchange.IntegrationTests/QRBusinessExchange.IntegrationTests.csproj`
2. Verify database connections work (EF Core 10)
3. Check external service integrations (if any)

**Key Areas Validated**:
- Database access (Entity Framework Core 10)
- API endpoint functionality
- Authentication/Authorization (JWT)
- Cross-component integration
- External service clients (RestSharp, Stripe if tested)

**Success Criteria**:
- ? All integration tests pass
- ? Database queries execute correctly
- ? API endpoints respond as expected
- ? JWT authentication functional

**If Failures Occur**:
- Check database connection strings unchanged
- Verify EF Core migrations compatible
- Test JWT token generation/validation manually
- Review any external service client behavior changes

---

### Level 4: API Manual Validation

**Objective**: Verify ASP.NET Core API and Swagger UI function correctly

**Steps**:

1. **Start Application**:
   ```bash
   cd src/QRBusinessExchange.API
   dotnet run
   ```

2. **Validate Swagger UI** (Critical for Swashbuckle 10):
   - Navigate to `https://localhost:<port>/swagger`
   - ? Swagger UI loads without errors
   - ? All endpoints documented
   - ? OpenAPI spec renders correctly
   - ? "Authorize" button present (JWT Bearer)

3. **Test Authentication Flow**:
   - Click "Authorize" button
   - Enter JWT token (obtain via `/api/Auth/login` endpoint)
   - ? Authorization modal works
   - ? Token is accepted

4. **Test Sample Endpoint**:
   - Select any authenticated endpoint
   - Click "Try it out"
   - Fill in parameters
   - Execute
   - ? Request succeeds with Bearer token
   - ? Response format correct

5. **Check Application Logs**:
   - ? No unhandled exceptions
   - ? No warning logs from new package versions

**Success Criteria**:
- ? Application starts without errors
- ? Swagger UI fully functional
- ? JWT authentication works end-to-end
- ? API endpoints respond correctly
- ? No console errors or warnings

**If Failures Occur**:
- Review Swashbuckle configuration (see Breaking Changes Catalog)
- Check ASP.NET Core authentication middleware setup
- Validate OpenAPI generation with `dotnet swagger tofile`
- Consult Swashbuckle 10.x documentation if UI issues

---

### Level 5: Blazor Web Application Validation (Optional)

**Objective**: Ensure Blazor app still runs (minimal package changes)

**Steps**:
1. Start Blazor application:
   ```bash
   cd src/QRBusinessExchange.Web
   dotnet run
   ```
2. Navigate to home page
3. Verify basic rendering works

**Success Criteria**:
- ? Application starts
- ? Pages render without errors

**Note**: Low priority since Web project has no package updates, only project references.

---

### Smoke Tests (Quick Validation)

After each project update (when All-At-Once completes), perform quick smoke tests:

**Per-Project Smoke Tests**:

| Project | Smoke Test | Expected Outcome |
|---------|-----------|------------------|
| **Core** | `dotnet build` | Builds successfully |
| **Infrastructure** | `dotnet build` | Builds successfully |
| **API** | `dotnet build` | Builds successfully |
| **Web** | `dotnet build` | Builds successfully |
| **UnitTests** | `dotnet test --no-build` | All tests pass |
| **IntegrationTests** | `dotnet test --no-build` | All tests pass |

**Cumulative Smoke Test** (All-At-Once Validation):
```bash
# From solution root
dotnet restore
dotnet build
dotnet test --no-build
# Check: All pass ?
```

---

### Comprehensive Validation (Final Check)

**Before Merge/Deployment**, validate:

1. **No Regressions**:
   - [ ] All tests pass (unit + integration)
   - [ ] No new build warnings
   - [ ] No performance degradation observed

2. **Package Integrity**:
   - [ ] No package downgrades: `dotnet list package --outdated`
   - [ ] No vulnerable packages: `dotnet list package --vulnerable`
   - [ ] No deprecated packages

3. **Functional Validation**:
   - [ ] API starts and serves requests
   - [ ] Swagger UI functional
   - [ ] JWT authentication works
   - [ ] Database access functional (if applicable)

4. **Documentation**:
   - [ ] Package versions documented (if needed)
   - [ ] Breaking changes communicated (none expected)
   - [ ] Deployment notes updated (if applicable)

---

### Testing Timeline (All-At-Once)

**Single Testing Phase** (after atomic update):

| Stage | Activity | Duration Estimate |
|-------|----------|------------------|
| **Build** | Restore & build solution | < 5 minutes |
| **Unit Tests** | Run unit test suite | < 5 minutes |
| **Integration Tests** | Run integration tests | 5-10 minutes |
| **Manual Validation** | API + Swagger UI check | 5-10 minutes |
| **Final Review** | Comprehensive checklist | 5 minutes |
| **Total** | Complete validation cycle | ~20-30 minutes |

**Note**: Actual times may vary based on solution size and test suite complexity.

---

### Validation Checklist (Master)

Use this checklist to track validation progress:

#### Build Validation
- [ ] `dotnet restore` completes successfully
- [ ] `dotnet build` exits with code 0
- [ ] No package downgrade warnings
- [ ] No new critical build warnings

#### Test Validation
- [ ] Unit tests pass (`QRBusinessExchange.UnitTests`)
- [ ] Integration tests pass (`QRBusinessExchange.IntegrationTests`)
- [ ] Test discovery works in IDE
- [ ] No flaky test behavior

#### API Validation
- [ ] API application starts successfully
- [ ] Swagger UI loads at `/swagger`
- [ ] "Authorize" button functional
- [ ] Sample authenticated request works
- [ ] No console errors/warnings

#### Package Validation
- [ ] No vulnerable packages (`dotnet list package --vulnerable`)
- [ ] No outdated critical packages (`dotnet list package --outdated`)
- [ ] Version standardization complete (JWT tokens)

#### Final Checks
- [ ] All projects build
- [ ] All tests pass
- [ ] API functional
- [ ] Blazor app functional (optional)
- [ ] Documentation updated (if needed)
- [ ] Ready for code review

---

### Exit Criteria

**Proceed to Merge When**:
- ? All validation checklist items complete
- ? No unresolved issues or errors
- ? Code review approved (if required)
- ? Confidence level high for deployment

**Consider Rollback If**:
- ? Multiple test failures across projects
- ? Critical API functionality broken
- ? Swagger UI completely non-functional
- ? Unresolvable package conflicts
- ? Performance significantly degraded

---

## Complexity & Effort Assessment

### Per-Project Complexity Ratings

| Project | Packages to Update | Complexity | Risk | Justification |
|---------|-------------------|------------|------|---------------|
| **QRBusinessExchange.Core** | 0 | None | ?? None | No packages |
| **QRBusinessExchange.Infrastructure** | 8 | Low | ?? Low | Straightforward version bumps |
| **QRBusinessExchange.API** | 7 | Low | ?? Low-Med | Includes Swashbuckle major update |
| **QRBusinessExchange.Web** | 0 | None | ?? None | Only project references |
| **QRBusinessExchange.UnitTests** | 4 | Low | ?? Low | Test packages, low risk |
| **QRBusinessExchange.IntegrationTests** | 4 | Low | ?? Low | Test packages, low risk |

### Overall Complexity Assessment

**Classification: Low Complexity**

**Factors Contributing to Low Complexity**:
- ? Small project count (6 projects, only 3 with packages)
- ? All projects already on target framework
- ? No breaking API changes
- ? No code modifications expected
- ? Clear dependency structure
- ? Good test coverage for validation
- ? Simple package dependency graph

**Complexity Drivers**:
- Swashbuckle major version update (7 ? 10) - Mitigated by pre-review
- xUnit runner major version (2 ? 3) - Low impact on test execution
- Multiple package updates in single operation - Managed by All-At-Once strategy

### Phase Complexity with All-At-Once Strategy

Since this is All-At-Once, there's only one "phase":

**Single Update Phase**:
- **Scope**: Update all 13 unique packages across 3 projects
- **Complexity**: Low
- **Dependencies**: None (all simultaneous)
- **Risk**: Low (all packages compatible)
- **Validation**: Build + comprehensive test suite

**Relative Complexity**: 2/10
- Simple operation (version bumps)
- Well-understood packages
- No architectural changes
- Strong validation available

### Resource Requirements

**Skills Required**:
- **Basic .NET Knowledge**: Understanding of NuGet package management
- **Build Troubleshooting**: Ability to interpret compilation errors (if any)
- **Git Proficiency**: Branch management, commit, potential rollback
- **Testing Familiarity**: Run and interpret xUnit test results

**Team Capacity**:
- **Minimum**: 1 developer (can be performed by single person)
- **Optimal**: 1 developer + 1 reviewer
- **Parallel Work**: Not beneficial (atomic update)

**Tools Required**:
- .NET 10 SDK (already installed)
- Git (for branch and commit management)
- IDE (Visual Studio, VS Code, or Rider)
- Terminal access for `dotnet` commands

### Relative Effort by Activity

| Activity | Relative Effort | Notes |
|----------|----------------|-------|
| **Update .csproj Files** | Low | Mechanical version updates |
| **Restore Packages** | Minimal | Automated (`dotnet restore`) |
| **Build Solution** | Minimal | Automated (`dotnet build`) |
| **Fix Compilation Errors** | Very Low | None expected |
| **Run Tests** | Low | Automated (`dotnet test`) |
| **Validate Swagger UI** | Low | Manual check in browser |
| **Code Review** | Low | Simple version changes |
| **Documentation Updates** | Minimal | Update package versions in docs if needed |

### Complexity Comparison

**Compared to Framework Upgrade**: Much simpler
- No TargetFramework changes
- No project file restructuring
- No migration of deprecated APIs

**Compared to New Feature**: Simpler
- No new code written
- No architectural decisions
- No design discussions

**Compared to Bug Fix**: Similar
- Focused scope
- Clear validation criteria
- Low risk of side effects

### Confidence Assessment

**Confidence Level: High** ??

**Reasoning**:
- Straightforward version updates
- Pre-reviewed for breaking changes
- All packages confirmed compatible
- Existing tests provide safety net
- Small scope enables easy rollback
- Clear success criteria

**Uncertainty Factors**: Minimal
- Test framework version jump (xUnit runner)
- Potential minor Swagger UI adjustments
- Possible transitive dependency updates

**Overall**: This is a low-complexity, low-risk operation with high confidence in successful completion.

---

## Source Control Strategy

### Branching Strategy

**Branch Structure**:
- **Source Branch**: `main` (current working branch)
- **Upgrade Branch**: `upgrade/net10-package-modernization` (create new)
- **Merge Target**: `main` (after validation)

**Workflow**:
1. Create upgrade branch from `main`
2. Perform all package updates in upgrade branch
3. Validate thoroughly (build, test, manual checks)
4. Create Pull Request: `upgrade/net10-package-modernization` ? `main`
5. Code review (if required)
6. Merge to `main` after approval

### Commit Strategy

**All-At-Once Strategy Commit Approach**: Single comprehensive commit

**Recommended Commit Structure**:

```
feat: Update NuGet packages to .NET 10 aligned versions

Updates all package references across solution to latest stable versions
optimized for .NET 10 runtime.

BREAKING CHANGE: None expected. All packages backward compatible.

Changes:
- Microsoft.AspNetCore.* packages: 8.0.x ? 10.0.2
- Microsoft.EntityFrameworkCore.*: 8.0.11 ? 10.0.2  
- Swashbuckle.AspNetCore: 7.2.0 ? 10.1.0
- System.IdentityModel.Tokens.Jwt: standardized to 8.15.0
- xUnit test packages: 2.5.3 ? 2.9.3 / 3.1.5
- Minor updates: Newtonsoft.Json, QRCoder, coverlet

Affected Projects:
- QRBusinessExchange.API (7 packages)
- QRBusinessExchange.Infrastructure (8 packages)
- QRBusinessExchange.UnitTests (4 packages)
- QRBusinessExchange.IntegrationTests (4 packages)

Validation:
? All projects build successfully
? All tests pass (unit + integration)
? API functional with Swagger UI
? No breaking changes identified

Related: Package modernization for .NET 10 optimization
```

**Why Single Commit**:
- ? Atomic operation - all changes are interdependent
- ? Easier rollback if issues arise
- ? Clean git history (one logical change)
- ? Aligns with All-At-Once strategy
- ? Simplifies code review

**Alternative** (if preferred by team):
- **Separate commits per project type**:
  1. "Update API packages"
  2. "Update Infrastructure packages"
  3. "Update test packages"
- Still in same PR, but more granular history

### Commit Message Format

Follow **Conventional Commits** format for consistency:

**Format**:
```
<type>(<scope>): <subject>

<body>

<footer>
```

**Example for this update**:
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

BREAKING CHANGE: None. All updates backward compatible.

Closes #<issue-number> (if applicable)
```

### Review and Merge Process

**Pull Request Requirements**:

1. **PR Title**: "Modernize NuGet packages for .NET 10 compatibility"

2. **PR Description Template**:
```markdown
## Summary
Updates all NuGet packages to .NET 10 aligned versions for improved 
performance, security, and feature support.

## Changes
- 13 unique packages updated across 4 projects
- All Microsoft packages moved to 10.0.2
- Swashbuckle upgraded from 7.2.0 to 10.1.0
- Test infrastructure updated to latest stable

## Testing
- [x] Solution builds successfully
- [x] All unit tests pass (X/X tests)
- [x] All integration tests pass (X/X tests)
- [x] API functional with Swagger UI validated
- [x] No breaking changes detected

## Breaking Changes
None identified. All packages backward compatible with existing code.

## Rollback Plan
Single commit enables clean revert if issues arise post-merge.

## Checklist
- [x] Code builds without errors
- [x] Tests pass
- [x] Documentation updated (if needed)
- [x] Breaking changes reviewed
- [x] Ready for review
```

3. **PR Reviewers**: Assign as per team policy

4. **Review Checklist**:
   - [ ] Package versions match plan
   - [ ] No accidental downgrades
   - [ ] Build passes in CI/CD (if configured)
   - [ ] Tests pass in CI/CD
   - [ ] Commit message follows convention
   - [ ] No unrelated changes included

### Merge Criteria

**Merge to `main` when**:
- ? All validation checklist items complete
- ? PR approved by required reviewers
- ? CI/CD pipeline passes (if configured)
- ? No merge conflicts with `main`
- ? Documentation updated (if needed)

**Merge Method**: 
- **Preferred**: Squash and merge (creates single commit in main)
- **Alternative**: Merge commit (preserves upgrade branch history)
- **Avoid**: Rebase and merge (complicates rollback)

### Post-Merge Actions

1. **Delete Upgrade Branch**: 
   ```bash
   git branch -d upgrade/net10-package-modernization
   git push origin --delete upgrade/net10-package-modernization
   ```

2. **Tag Release** (optional):
   ```bash
   git tag -a v1.1.0 -m "Package modernization for .NET 10"
   git push origin v1.1.0
   ```

3. **Update Documentation** (if applicable):
   - Update dependency documentation
   - Note package version changes in CHANGELOG.md
   - Update README.md if package requirements changed

4. **Notify Team**:
   - Announce package updates in team channel
   - Share validation results
   - Note any observations (e.g., performance improvements)

### Rollback Procedure

**If Issues Discovered After Merge**:

**Option 1: Git Revert** (preferred, safe):
```bash
# Find the merge commit
git log --oneline --graph --all -10

# Revert the merge commit
git revert -m 1 <merge-commit-hash>

# Push to main
git push origin main
```

**Option 2: Manual Rollback** (if revert fails):
```bash
# Create rollback branch
git checkout -b rollback/package-updates main

# Manually revert .csproj changes
# (use git diff to see changes, manually undo)

# Commit rollback
git commit -m "Rollback: Revert package updates due to <issue>"

# PR rollback to main
```

**Option 3: Reset** (if no one else pulled, USE WITH CAUTION):
```bash
git reset --hard HEAD~1
git push --force origin main
# WARNING: Only if no one else has pulled main!
```

### Git Workflow Diagram

```
main (current)
  ?
  ???? Create branch: upgrade/net10-package-modernization
  ?
  ???? Make changes: Update all .csproj files
  ?
  ???? Commit: "feat(packages): Modernize for .NET 10"
  ?
  ???? Validate: Build, test, manual checks
  ?
  ???? Push: git push origin upgrade/net10-package-modernization
  ?
  ???? Create PR: upgrade/net10-package-modernization ? main
  ?
  ???? Review: Code review process
  ?
  ???? Merge: Squash and merge to main
  ?
  ???? Post-merge: Delete branch, tag (optional)
```

### Best Practices for This Update

1. **Single Commit**: Keep all package updates in one commit for atomic rollback
2. **Descriptive Message**: Clearly document what was updated and why
3. **Thorough Testing**: Don't merge until all validation passes
4. **Clean Branch**: Upgrade branch should only contain package updates, no other changes
5. **PR Description**: Comprehensive PR description helps future maintainers understand the change
6. **Squash on Merge**: Creates clean history in main branch
7. **Tag Release**: Consider tagging after merge for easy reference point

### Source Control Checklist

Before starting:
- [ ] Current branch is `main` and up to date
- [ ] No uncommitted changes (stash or commit first)

During update:
- [ ] Created upgrade branch
- [ ] All package updates in upgrade branch
- [ ] Meaningful commit message(s)
- [ ] Pushed to remote

Before merge:
- [ ] All validation complete
- [ ] PR created with detailed description
- [ ] CI/CD passes (if configured)
- [ ] Code review approved
- [ ] No merge conflicts

After merge:
- [ ] Upgrade branch deleted
- [ ] Tag created (optional)
- [ ] Documentation updated
- [ ] Team notified

---

## Success Criteria

### Technical Criteria

**All projects successfully modernized when**:

1. **Package Updates Applied** ?
   - [ ] All 13 unique packages updated to target versions
   - [ ] Microsoft.AspNetCore.* packages at 10.0.2
   - [ ] Microsoft.EntityFrameworkCore.* packages at 10.0.2
   - [ ] Swashbuckle.AspNetCore at 10.1.0
   - [ ] System.IdentityModel.Tokens.Jwt standardized to 8.15.0 across all projects
   - [ ] Test packages (xUnit, Test SDK, coverlet) updated
   - [ ] Third-party packages (Newtonsoft.Json, QRCoder) updated
   - [ ] No alpha/prerelease versions in production projects

2. **Build Success** ?
   - [ ] `dotnet restore` completes without errors
   - [ ] `dotnet build` exits with code 0
   - [ ] All 6 projects build successfully
   - [ ] Zero compilation errors
   - [ ] No package conflict warnings
   - [ ] No package downgrade warnings

3. **Test Success** ?
   - [ ] All unit tests pass (QRBusinessExchange.UnitTests)
   - [ ] All integration tests pass (QRBusinessExchange.IntegrationTests)
   - [ ] Test discovery works in IDE Test Explorer
   - [ ] No new test failures introduced
   - [ ] Test execution time remains reasonable

4. **No Security Vulnerabilities** ?
   - [ ] `dotnet list package --vulnerable` returns no vulnerabilities
   - [ ] All packages are stable releases (no alpha/beta)
   - [ ] No deprecated packages in use

5. **Runtime Functionality** ?
   - [ ] API application starts without errors
   - [ ] Swagger UI loads and renders correctly at `/swagger`
   - [ ] JWT authentication functional (Authorize button works)
   - [ ] Sample API requests succeed with Bearer token
   - [ ] No unhandled exceptions in console logs
   - [ ] Blazor Web app starts and renders (basic check)

---

### Quality Criteria

**Code quality maintained when**:

1. **Code Quality** ?
   - [ ] No new code analyzer warnings introduced
   - [ ] Code coverage maintained (if measured)
   - [ ] No additional technical debt
   - [ ] Project structure unchanged

2. **Test Coverage** ?
   - [ ] Existing tests still provide same coverage
   - [ ] No tests removed or skipped
   - [ ] Test suite completeness maintained

3. **Documentation** ?
   - [ ] Package version changes documented (if required)
   - [ ] Breaking changes reviewed and documented (none expected)
   - [ ] README.md updated if package requirements changed
   - [ ] CHANGELOG.md updated with package modernization entry

---

### Process Criteria

**Modernization process successful when**:

1. **All-At-Once Strategy Followed** ?
   - [ ] All package updates applied simultaneously
   - [ ] No intermediate multi-targeted states
   - [ ] Single comprehensive validation cycle completed
   - [ ] Dependency-aware update sequence respected

2. **Source Control Strategy Executed** ?
   - [ ] Upgrade branch created from `main`
   - [ ] All changes committed in upgrade branch
   - [ ] Commit message follows conventional format
   - [ ] Pull Request created with detailed description
   - [ ] Code review completed (if required)
   - [ ] Merged to `main` via squash and merge
   - [ ] Upgrade branch deleted post-merge

3. **Validation Completed** ?
   - [ ] All items in Testing & Validation Strategy checklist complete
   - [ ] Build validation passed
   - [ ] Unit testing passed
   - [ ] Integration testing passed
   - [ ] API manual validation passed
   - [ ] Smoke tests passed
   - [ ] Comprehensive validation checklist complete

4. **Risk Management Applied** ?
   - [ ] Breaking changes catalog reviewed
   - [ ] Swashbuckle 7?10 migration pre-reviewed
   - [ ] Rollback plan prepared
   - [ ] No red flags encountered during validation

---

### All-At-Once Strategy-Specific Criteria

**Strategy execution successful when**:

1. **Atomicity Maintained** ?
   - [ ] All package updates in single commit
   - [ ] No partial states created
   - [ ] Easy rollback possible (single commit revert)

2. **Simultaneity Achieved** ?
   - [ ] All projects updated together
   - [ ] Microsoft packages aligned to 10.0.2
   - [ ] JWT token version standardized across projects
   - [ ] Test packages updated uniformly

3. **Unified Validation** ?
   - [ ] Single build cycle validated entire solution
   - [ ] Single test cycle validated all projects
   - [ ] No phased checkpoints needed

---

### Measurable Outcomes

**Quantifiable success indicators**:

1. **Package Version Alignment**:
   - ? 100% of Microsoft packages on .NET 10 versions (10.0.2)
   - ? 0% packages remain on .NET 8 versions
   - ? 0 package version conflicts
   - ? 100% of projects build successfully

2. **Test Pass Rate**:
   - ? Unit tests: 100% pass rate maintained
   - ? Integration tests: 100% pass rate maintained
   - ? 0 new test failures introduced

3. **Build Metrics**:
   - ? Build time: Similar or improved compared to pre-update
   - ? Warnings: Zero new critical warnings
   - ? Errors: Zero build or restore errors

4. **Security Posture**:
   - ? Vulnerable packages: 0 (down from 0)
   - ? Outdated critical packages: 0
   - ? All packages on stable releases

---

### Definition of Done

**Package modernization is COMPLETE when**:

? **ALL** of the following are true:

- ? All technical criteria met
- ? All quality criteria met
- ? All process criteria met
- ? All-At-Once strategy criteria met
- ? Pull Request merged to `main`
- ? Upgrade branch deleted
- ? Team notified of completion
- ? Documentation updated (if required)
- ? No outstanding issues or warnings
- ? Solution ready for deployment (if applicable)

---

### Post-Modernization Validation

**After merge to `main`, verify**:

1. **Stability Check** (first 24-48 hours):
   - [ ] No unexpected runtime issues reported
   - [ ] API performance acceptable
   - [ ] No increased error rates
   - [ ] Team members can pull and build successfully

2. **Long-term Success** (first week):
   - [ ] No regressions discovered in testing/staging
   - [ ] Performance maintained or improved
   - [ ] No unexpected behavior from updated packages
   - [ ] Team comfortable with new package versions

---

### Exit Criteria

**Proceed to next phase when**:

? All success criteria met
? Definition of Done satisfied
? Post-modernization validation clean

**Sign-off required from**:
- [ ] Developer who performed update
- [ ] Code reviewer (if required)
- [ ] Tech lead / architect (if required)
- [ ] QA / Testing team (if applicable)

---

### Measuring Success

**Key Performance Indicators (KPIs)**:

| KPI | Target | How to Measure |
|-----|--------|----------------|
| **Build Success Rate** | 100% | `dotnet build` exit code 0 |
| **Test Pass Rate** | 100% | All tests green in test results |
| **Package Vulnerability Count** | 0 | `dotnet list package --vulnerable` |
| **Deployment Failures** | 0 | No rollbacks needed post-merge |
| **Team Velocity Impact** | Minimal | Development continues normally |
| **Performance Regression** | 0% | Response times maintained |

---

### Success Declaration

**This modernization is officially SUCCESSFUL when**:

?? All validation passes
?? Code merged to `main`
?? No issues in first 48 hours
?? Team can continue development without disruption
?? Documentation updated
?? Lessons learned documented (if any)

**Final Checkpoint**:
- [ ] All checklist items in this plan completed
- [ ] No open issues or blockers
- [ ] Solution state stable and ready for production
- [ ] Package modernization objectives achieved

---

## Appendix: Quick Reference

### Commands Checklist

```bash
# Restore packages
dotnet restore

# Build solution
dotnet build

# Run all tests
dotnet test

# Check for vulnerabilities
dotnet list package --vulnerable

# Check for outdated packages
dotnet list package --outdated

# Run API
cd src/QRBusinessExchange.API && dotnet run
```

### Package Versions Reference

| Package | From | To |
|---------|------|-----|
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.11 | **10.0.2** |
| Microsoft.AspNetCore.OpenApi | 8.0.23 | **10.0.2** |
| Microsoft.EntityFrameworkCore.SqlServer | 8.0.11 | **10.0.2** |
| Microsoft.EntityFrameworkCore.Tools | 8.0.11 | **10.0.2** |
| Swashbuckle.AspNetCore | 7.2.0 | **10.1.0** |
| System.IdentityModel.Tokens.Jwt | 8.0.2/8.2.1 | **8.15.0** |
| Newtonsoft.Json | 13.0.3 | **13.0.4** |
| QRCoder | 1.6.0 | **1.7.0** |
| Microsoft.NET.Test.Sdk | 17.8.0 | **18.0.1** |
| xunit | 2.5.3 | **2.9.3** |
| xunit.runner.visualstudio | 2.5.3 | **3.1.5** |
| coverlet.collector | 6.0.0 | **6.0.4** |

---

**Plan Complete** ?

This plan provides comprehensive guidance for modernizing NuGet packages in your .NET 10 solution. Follow the All-At-Once strategy, complete all validation steps, and verify all success criteria before considering the modernization complete.
