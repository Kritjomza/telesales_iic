# Secure User Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add secure Admin and Super Admin user CRUD to Master Data / Users with privilege-escalation prevention, safe password handling, reference-aware deletion, and complete tests.

**Architecture:** Extend the existing `UsersController` with typed request/response DTOs and explicit policy helpers, while preserving the current GET scope except for removing `linetoken`. Extend the existing `MasterDataView` drawer and API service rather than creating a new page or generic CRUD layer.

**Tech Stack:** ASP.NET Core 8, EF Core 8, Pomelo MySQL, BCrypt.Net-Next, xUnit, React 19, TypeScript, Vitest, Testing Library.

## Global Constraints

- No database schema change.
- Backend authorization is authoritative; frontend controls are UX only.
- Super Admin may manage every schema-supported role.
- Admin cannot create/promote to Super Admin, edit/delete an existing Super Admin, or change their own role.
- Use 401/403/404/409 and existing 400 validation conventions exactly as documented.
- Do not cascade or soft-delete users; hard-delete only unreferenced users.
- Never return passwords, password hashes, tokens, or authentication internals.
- Preserve unrelated APIs, pages, authentication flows, reports, customers, imports, and master-data modules.
- Preserve existing unrelated working-tree changes.

---

### Task 1: Backend User Contracts, Authorization, Validation, and Password Behavior

**Files:**
- Create: `Backend/Telesale.Api.Tests/UsersControllerTests.cs`
- Modify: `Backend/Telesale.Api/Controllers/UsersController.cs`

**Interfaces:**
- Produces: `CreateUserRequest`, `UpdateUserRequest`, and `UserResponse` DTOs.
- Produces: `POST /api/users` and `PUT /api/users/{id}`.
- Uses: `User.IsAdmin()`, `User.GetUserRole()`, `User.GetUserId()`, `AppRoles.Normalize`, and `BCrypt.Net.BCrypt`.

- [ ] **Step 1: Write failing authorization and response-secrecy tests**

Add test helpers using `TelesaleDbContext` with a unique EF InMemory database and principals containing `NameIdentifier` and `Role` claims. Add theories for Manager, Supervisor, Sale, Tele Sale, and Viewer that call create/update and assert `ForbidResult`; add Admin and Super Admin success cases. Reflect over each write action for `[Authorize]` coverage at the controller level and verify safe response serialization contains no `password`, `linetoken`, `remember_token`, `failed_login_count`, or `locked_until`.

```csharp
[Theory]
[InlineData(AppRoles.Manager)]
[InlineData(AppRoles.Sale)]
[InlineData(AppRoles.TeleSale)]
public async Task CreateUser_NonAdmin_ReturnsForbid(string role)
{
    await using var db = CreateDb();
    var controller = CreateController(db, 10, role);
    var result = await controller.CreateUser(ValidCreateRequest(), default);
    Assert.IsType<ForbidResult>(result);
}
```

- [ ] **Step 2: Run the focused backend tests and verify RED**

Run: `dotnet test Backend/Telesale.Api.Tests/Telesale.Api.Tests.csproj --filter FullyQualifiedName~UsersControllerTests`

Expected: compilation failures because the new actions and DTOs do not exist.

- [ ] **Step 3: Add typed DTOs, safe projection, and backend write gate**

Define DTOs with nullable update password and explicit safe fields:

```csharp
public sealed class CreateUserRequest
{
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Tel { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateUserRequest
{
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? Password { get; set; }
    public string? Tel { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; }
}

public sealed record UserResponse(
    uint Id, string Name, string Username, string Email, string Role,
    string Tel, string Position, bool IsActive);
```

At the start of every write action:

```csharp
if (!User.IsAdmin()) return Forbid();
```

Keep `[Authorize]` on the controller so unauthenticated requests are challenged by middleware.

- [ ] **Step 4: Add failing validation, duplicate, privilege, and password tests**

Cover required/trimmed fields, valid email, 255-character schema limits, roles in the actual MySQL enum, required creation password, password policy, duplicate username/email, Admin create/promotion to Super Admin, Admin edit of existing Super Admin, Admin self-role change, blank update password preservation, and supplied update password replacement.

```csharp
[Fact]
public async Task UpdateUser_BlankPassword_PreservesHash()
{
    await using var db = CreateDb();
    var existingHash = BCrypt.Net.BCrypt.HashPassword("ExistingPass1!");
    db.users.Add(UserEntity(20, AppRoles.Sale, existingHash));
    await db.SaveChangesAsync();
    var controller = CreateController(db, 1, AppRoles.SuperAdmin);

    var request = ValidUpdateRequest();
    request.Password = "   ";
    var result = await controller.UpdateUser(20, request, default);

    Assert.IsType<OkObjectResult>(result);
    Assert.Equal(existingHash, db.users.Single(u => u.id == 20).password);
}
```

- [ ] **Step 5: Run the focused tests and verify RED**

Run the same filtered `dotnet test` command.

Expected: assertion failures for missing validation, privilege rules, duplicates, and password behavior.

- [ ] **Step 6: Implement minimal create/update behavior**

Normalize input once, validate using a shared helper, query duplicates with `AnyAsync`, and return `Conflict(new { message = ... })`. Apply Admin restrictions before mutation. For create:

```csharp
password = BCrypt.Net.BCrypt.HashPassword(request.Password),
created_at = DateTime.UtcNow,
updated_at = DateTime.UtcNow
```

For update:

```csharp
if (!string.IsNullOrWhiteSpace(request.Password))
    target.password = BCrypt.Net.BCrypt.HashPassword(request.Password);
```

Never hash blank input. Return `UserResponse` only.

- [ ] **Step 7: Handle actual MySQL uniqueness races**

Add a focused helper that walks `DbUpdateException.InnerException` messages and recognizes only MySQL duplicate-entry error 1062 together with `users_username_unique` or `users_email_unique`. Return 409 only for those cases and rethrow unrelated update exceptions.

```csharp
private static bool IsUsersUniqueViolation(DbUpdateException ex)
{
    for (Exception? current = ex; current != null; current = current.InnerException)
    {
        if (current.Message.Contains("1062", StringComparison.OrdinalIgnoreCase) &&
            (current.Message.Contains("users_username_unique", StringComparison.OrdinalIgnoreCase) ||
             current.Message.Contains("users_email_unique", StringComparison.OrdinalIgnoreCase)))
            return true;
    }
    return false;
}
```

- [ ] **Step 8: Run focused and full backend tests**

Run:

```text
dotnet test Backend/Telesale.Api.Tests/Telesale.Api.Tests.csproj --filter FullyQualifiedName~UsersControllerTests
dotnet test Backend/Telesale.Api.Tests/Telesale.Api.Tests.csproj
```

Expected: all tests pass with zero failures.

- [ ] **Step 9: Commit backend create/update**

```text
git add Backend/Telesale.Api/Controllers/UsersController.cs Backend/Telesale.Api.Tests/UsersControllerTests.cs
git commit -m "feat: add secure user create and update"
```

### Task 2: Reference-Aware User Deletion

**Files:**
- Modify: `Backend/Telesale.Api/Controllers/UsersController.cs`
- Modify: `Backend/Telesale.Api.Tests/UsersControllerTests.cs`

**Interfaces:**
- Produces: `DELETE /api/users/{id}`.
- Consumes EF sets: `customers`, `targets`, `import_sessions`, and `assignment_histories`.

- [ ] **Step 1: Confirm every repository user reference**

Search models, `TelesaleDbContext`, SQL backup/schema files, raw SQL initializer, and controllers:

```text
rg -n -i "user_id|created_by|updated_by|imported_by|sale_id|telesale_id|old_sale_id|new_sale_id|old_telesale_id|new_telesale_id|references.*users" Backend myserver
```

Record the resulting protected fields in a comment beside the deletion predicate. The current review has identified customer `sale_id`, `telesale_id`, `sale_id_bak`, `telesale_id_bak`; target `user_id`; import session `imported_by`; and assignment-history old/new sale/telesale IDs.

- [ ] **Step 2: Write failing deletion tests**

Cover Super Admin and Admin success for unreferenced eligible users; other roles 403; Admin targeting Super Admin 403; missing user 404; current user 409; and one test for every protected reference field returning 409 without deleting either user or business data.

```csharp
[Fact]
public async Task DeleteUser_WhenReferencedByCustomer_ReturnsConflict()
{
    await using var db = CreateDb();
    db.users.Add(UserEntity(20, AppRoles.Sale));
    db.customers.Add(new customer { id = 1, name = "Protected", sale_id = 20, status = "New", create_type = "Key", is_active = true });
    await db.SaveChangesAsync();

    var result = await CreateController(db, 1, AppRoles.Admin).DeleteUser(20, default);

    Assert.IsType<ConflictObjectResult>(result);
    Assert.NotNull(await db.users.FindAsync((uint)20));
    Assert.NotNull(await db.customers.FindAsync(1));
}
```

- [ ] **Step 3: Run deletion tests and verify RED**

Run: `dotnet test Backend/Telesale.Api.Tests/Telesale.Api.Tests.csproj --filter FullyQualifiedName~UsersControllerTests`

Expected: failures because delete is absent.

- [ ] **Step 4: Implement ordered deletion safeguards**

Use this order: admin gate, target lookup/404, Admin-vs-Super-Admin/403, self check/409, all reference queries/409, then `Remove` and `SaveChangesAsync`. Do not configure cascade behavior and do not mutate references.

- [ ] **Step 5: Run focused and full backend tests**

Run the focused and full commands from Task 1.

Expected: all tests pass and protected records remain unchanged.

- [ ] **Step 6: Commit deletion safeguards**

```text
git add Backend/Telesale.Api/Controllers/UsersController.cs Backend/Telesale.Api.Tests/UsersControllerTests.cs
git commit -m "feat: safeguard user deletion"
```

### Task 3: Frontend API and Permission-Aware User Management UI

**Files:**
- Modify: `Frontend/src/domain/types.ts`
- Modify: `Frontend/src/domain/apiService.ts`
- Modify: `Frontend/src/domain/permissions.ts`
- Modify: `Frontend/src/views/MasterDataView.tsx`
- Create: `Frontend/src/views/MasterDataView.users.test.tsx`

**Interfaces:**
- Produces: `CreateUserInput`, `UpdateUserInput`, `apiService.addUser`, `apiService.updateUser`, and `apiService.deleteUser`.
- Consumes: safe user responses from Tasks 1–2 and existing `Drawer`, toast, row-action, and confirmation patterns.

- [ ] **Step 1: Write failing permission and form-opening tests**

Mock lookup and user API calls. Render `MasterDataView` with `tableType="users"` for Super Admin, Admin, and Sale. Assert privileged roles see Add User and eligible row actions; Sale sees none; Admin sees no actions for a Super Admin row and no Super Admin role option. Click Add/Edit and assert the existing Drawer opens. Assert edit password is empty and optional.

- [ ] **Step 2: Run frontend test and verify RED**

Run: `npm test -- --run Frontend/src/views/MasterDataView.users.test.tsx` from `Frontend`.

Expected: failures because Users has no controls or form.

- [ ] **Step 3: Add safe frontend types and API methods**

Update `User` with `is_active` and remove `linetoken`. Add:

```ts
export type CreateUserInput = Omit<User, "id"> & { password: string };
export type UpdateUserInput = Omit<User, "id"> & { password?: string };
```

Add service calls:

```ts
async addUser(input: CreateUserInput): Promise<User>
async updateUser(id: number, input: UpdateUserInput): Promise<User>
async deleteUser(id: number): Promise<boolean>
```

Use `/users`, `/users/${id}`, JSON bodies, and `skipForbiddenRedirect: true` for delete consistently with other master-data deletes.

- [ ] **Step 4: Add a focused user-management permission helper**

Add helpers that normalize roles and express actor/target rules:

```ts
export const canManageUsers = (role: string) =>
  ["Admin", "Super Admin"].includes(normalizeRole(role));

export const canManageUserTarget = (actorRole: string, targetRole: string) =>
  normalizeRole(actorRole) === "Super Admin" ||
  (normalizeRole(actorRole) === "Admin" && normalizeRole(targetRole) !== "Super Admin");
```

Use these helpers only for UX; never infer backend authorization from them.

- [ ] **Step 5: Implement the existing Drawer user form**

Show Add User for privileged actors. Add inputs for name, username, email, password, role, tel, position, and active status. Require password only when `activeItem` is null. Build the payload so blank update password is omitted. Add an Actions header/cell and eligible Edit/Delete buttons. Remove Line Token.

- [ ] **Step 6: Write failing submission, validation, pending, deletion, and error tests**

Test create/edit payloads, omitted blank update password, disabled Save during pending request, validation preventing create without password, success toast and reload, API error retained in the drawer, delete confirmation cancel/accept, delete pending state, conflict toast/error, and successful reload.

- [ ] **Step 7: Run frontend test and verify RED**

Run the focused Vitest command.

Expected: new state/error assertions fail.

- [ ] **Step 8: Implement operation state and error handling**

Add `isSaving`, `deletingUserId`, and `formError`. Do not close the Drawer on errors. Convert `ApiError` messages to visible form errors/toasts using the existing service error shape. Disable repeated Save/Delete actions while pending; clear errors when opening/closing.

- [ ] **Step 9: Run focused and full frontend tests plus build**

Run from `Frontend`:

```text
npm test -- --run src/views/MasterDataView.users.test.tsx
npm test
npm run build
```

Expected: all tests and TypeScript/Vite build pass.

- [ ] **Step 10: Commit frontend user management**

```text
git add Frontend/src/domain/types.ts Frontend/src/domain/apiService.ts Frontend/src/domain/permissions.ts Frontend/src/views/MasterDataView.tsx Frontend/src/views/MasterDataView.users.test.tsx
git commit -m "feat: manage users from master data"
```

### Task 4: Final Security and Compatibility Verification

**Files:**
- Modify only if verification identifies an in-scope defect.

**Interfaces:**
- Verifies all outputs from Tasks 1–3.

- [ ] **Step 1: Search for sensitive user response fields**

Run:

```text
rg -n "linetoken|password|remember_token|failed_login_count|locked_until" Backend/Telesale.Api/Controllers/UsersController.cs Frontend/src/views/MasterDataView.tsx Frontend/src/domain/types.ts
```

Expected: password appears only in request/hash handling; none of the sensitive fields appear in a response projection or Users table.

- [ ] **Step 2: Verify the complete backend**

Run: `dotnet test Backend/Telesale.Api.Tests/Telesale.Api.Tests.csproj`

Expected: zero failed tests.

- [ ] **Step 3: Verify the complete frontend**

Run from `Frontend`:

```text
npm test
npm run build
```

Expected: zero failed tests and a successful production build.

- [ ] **Step 4: Review the final diff and scope**

Run:

```text
git diff --check
git status --short
git diff HEAD~3 -- Backend/Telesale.Api/Controllers/UsersController.cs Backend/Telesale.Api.Tests/UsersControllerTests.cs Frontend/src/domain/types.ts Frontend/src/domain/apiService.ts Frontend/src/domain/permissions.ts Frontend/src/views/MasterDataView.tsx Frontend/src/views/MasterDataView.users.test.tsx
```

Expected: no whitespace errors, no unrelated files staged, no schema changes, and no changes to unrelated product behavior.
