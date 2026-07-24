# User Management Design

## Goal

Add secure user creation, editing, and deletion to the existing Master Data / Users page without changing unrelated modules or the database schema.

## Scope

The change extends the existing `UsersController`, frontend API service, and `MasterDataView` user table/drawer. It does not alter authentication flows, customer behavior, imports, reports, other master-data modules, or the overall page design.

## Authorization

All user write endpoints require cookie authentication and an Admin or Super Admin role.

- Unauthenticated requests return 401.
- Authenticated non-admin roles return 403.
- Super Admin may create, edit, and delete users of any schema-supported role, subject to deletion safeguards.
- Admin may manage eligible non-Super-Admin users.
- Admin must not create or promote a user to Super Admin.
- Admin must not edit or delete an existing Super Admin.
- Admin must not change their own role.
- These rules are authoritative on the backend. The frontend mirrors them only for usability.

## API

Extend `api/users` with:

- `POST /api/users`
- `PUT /api/users/{id}`
- `DELETE /api/users/{id}`

Create and update use typed request DTOs. Responses use an explicit safe user DTO/projection and never serialize the EF entity directly.

The error contract is:

- 400, following the existing controller validation convention, for invalid request fields.
- 401 for unauthenticated requests.
- 403 for insufficient privileges or forbidden role management.
- 404 when the target user does not exist.
- 409 for duplicate username/email, self-deletion, or deletion blocked by protected references.

Database uniqueness races are handled by inspecting the actual provider exception for the username/email unique index. Unrelated `DbUpdateException` failures are not mislabeled as duplicates.

## Data and Validation

The current `users` schema already supports the requirement; no migration is needed.

Creation requires:

- Name
- Username
- Email
- Supported database role
- Password

Update requires the editable profile fields and accepts an optional password.

Validation covers:

- Trimmed required values
- Existing column length limits
- Email format
- Roles supported by the current database enum: Admin, Super Admin, Manager, Tele Sale, and Sale
- Duplicate username and email, excluding the current record during update
- The password policy used by user management

The repository currently has BCrypt verification in login but no separate reusable password-policy or hashing service. User management will use the same `BCrypt.Net` implementation so hashes remain login-compatible. The password policy will be defined once in the Users controller/DTO validation path and shared by create and nonblank update handling rather than introducing a second hashing technology.

On create, the password is required and BCrypt-hashed before persistence. On update, a blank or omitted password leaves the existing hash unchanged; a nonblank password is validated and BCrypt-hashed. The frontend never receives or prefills a password or hash.

## Safe Responses and Existing GET Behavior

`GET /api/users` keeps its current authorization, active-user filtering, supervisor position filtering, and assignment-list behavior. Its projection is minimally corrected to remove `linetoken`, which is credential-like sensitive data. The Users frontend table removes the Line Token column.

Safe user responses include only fields needed by the page: ID, name, username, email, role, telephone, position, and active status.

## Deletion Safeguards

Deletion is a hard delete only for genuinely unreferenced users. It never cascades or deletes related business data.

Before implementation, all EF mappings, schema metadata available in the repository, models, raw SQL initializers, and user-ID usages will be reviewed. The deletion check will cover every discovered protected reference, including direct and historical user references such as customer assignments, targets, import sessions, and assignment history where applicable.

Deletion returns 409 when:

- The target is the currently authenticated user.
- Any protected business record references the target.

Admin receives 403 before deletion checks when targeting an existing Super Admin. A missing target returns 404. No soft-delete behavior will be introduced unless the relationship review proves hard deletion unsuitable; that finding must be reported before changing the design.

## Frontend

The existing Users table and Drawer pattern are retained.

- Admin and Super Admin see Add User.
- Eligible rows show Edit and Delete actions according to the current actor and target role.
- Admin does not receive controls that would manage existing Super Admin users, and the role selector does not offer Super Admin to Admin.
- The user form contains name, username, email, role, telephone, position, active status, and password.
- Password is required on create and empty/optional on edit.
- Opening edit never prefills a password.
- Create and edit disable submission while pending and show validation or API errors without closing the drawer.
- Successful operations close the drawer, refresh the table, and use the existing toast mechanism.
- Delete uses the existing confirmation convention, prevents repeated submission while pending, refreshes on success, and shows conflict/API errors on failure.
- Backend enforcement remains authoritative if frontend controls are bypassed.

## Testing

Backend controller tests will prove:

- Super Admin can create, edit, and delete eligible users.
- Admin can create, edit, and delete eligible non-Super-Admin users.
- Admin privilege-escalation restrictions return 403.
- Other authenticated roles receive 403 on every write endpoint.
- The authorization pipeline declares authentication requirements so unauthenticated requests receive 401.
- Creation requires and hashes a valid password.
- Blank/omitted update password preserves the existing hash.
- A supplied update password is hashed and login-compatible.
- Invalid fields and unsupported roles are rejected.
- Duplicate username/email checks and database uniqueness races return 409.
- Missing users return 404.
- Self-deletion and referenced-user deletion return 409.
- Safe responses never expose password hashes or sensitive authentication fields.

Frontend tests will cover:

- Permission-based Add/Edit/Delete controls.
- Admin restrictions for Super Admin targets and role choices.
- Opening create and edit forms.
- Empty edit-password behavior and create-password requirement.
- Create/edit payloads and pending states.
- Delete confirmation and pending behavior.
- Client validation and API error/conflict handling.
- Refresh and success toasts after successful operations.

## Compatibility Impact

- `GET /api/users` no longer returns `linetoken`; the Users table no longer displays it.
- User write endpoints are newly added.
- Existing role names, authentication cookies, database schema, GET scoping, and unrelated modules remain unchanged.
