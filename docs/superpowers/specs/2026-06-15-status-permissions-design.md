# Telesurvey Status and Record Permissions Design

## Goal

Make Customer, Device, and Project statuses consistent across the React UI, ASP.NET API, Entity Framework mappings, and MySQL schema, while enforcing Telesurvey ownership and delete restrictions for Sale and Tele Sale users.

## Scope

This change covers Customer records and their Contact, Device, and Project descendants. It does not add notifications, workflow tables, or a general RBAC framework.

Admin and Super Admin retain their current read, create, edit, assignment, and delete behavior. Existing Manager and Supervisor behavior remains unchanged except that responses use the same clear API error format.

## Canonical Status Values

The application uses these exact, case-sensitive values:

- Customer: `New`, `Assigned`, `Booking`, `Wait`, `Sent`, `Win`, `Lost`
- Device: `New`, `Booking`, `Win`, `Lost`
- Project: `Discuss`, `Quotation`, `Win`, `Lost`, `Hold`, `Cancel`

`REPLACE` is removed from Customer UI choices and rejected by new API writes.

The existing Project database enum values `Disscuss` and `Quatation` are migrated to `Discuss` and `Quotation`. Existing rows are converted during startup before the enum definition is tightened.

## Backend Design

Create one focused status policy class containing the approved values and reusable validation helpers. Customer create and system actions continue to generate approved statuses internally. Customer update, Device create/update, and Project create/update validate any submitted status before modifying entities.

Invalid status input returns HTTP 400 with a JSON body:

```json
{
  "message": "Invalid project status 'New'. Allowed values: Discuss, Quotation, Win, Lost, Hold, Cancel."
}
```

Authorization remains based on the existing authenticated user claims and customer ownership fields:

- Sale may access a customer when `owner_id` or `sale_id` matches the current user.
- Tele Sale may access a customer when `owner_id` or `telesale_id` matches the current user.
- Descendant Contact, Device, and Project access is derived through the parent Customer.
- Sale and Tele Sale may view, add, and edit only accessible records.
- Sale and Tele Sale may never delete Customer, Contact, Device, or Project records, including records they own.
- Admin and Super Admin behavior is unchanged.
- Manager and Supervisor behavior is preserved by the existing team-scoping rules.

Denied operations return HTTP 403 with a JSON body:

```json
{
  "message": "You do not have permission to delete customer records."
}
```

Records outside a non-admin user's scope continue to return 403 rather than revealing whether the record exists.

## Database Compatibility

Startup initialization performs an idempotent Project status migration:

1. Temporarily widen `detail_pj.progress_status` to a string column.
2. Convert `Disscuss` to `Discuss` and `Quatation` to `Quotation`.
3. Replace null or unsupported legacy values with `Discuss`.
4. Alter the column to the canonical enum with default `Discuss`.

Entity Framework maps the Project property to the canonical enum. Customer and Device remain string-backed database columns, with the API status policy providing server-side enforcement.

## Frontend Design

Define shared TypeScript status constants and union types. CustomerManageView and BookingView render options from those constants so submitted values exactly match the API and database values.

Sale and Tele Sale users do not see delete controls for Customer, Contact, Device, or Project rows. This is usability only; the backend remains authoritative.

The API service exposes an error carrying the HTTP status and server message. Action-level 403 responses are shown through existing toast handling and do not globally navigate the user to the forbidden page. Authentication expiry continues to use the global 401 callback.

## Testing

Backend unit tests cover:

- Every approved status set.
- Rejection and error messages for invalid status strings.
- Sale and Tele Sale access to owned or assigned customers.
- Denial of access to unrelated customers.
- Agent delete permission denial.
- Preservation of Admin and Super Admin access.

Frontend tests cover:

- Canonical status constants.
- Delete capability by role.
- API error status and message parsing for 400 and 403 responses.

Verification includes the full frontend test suite and build, plus the backend test suite and build.

## Out of Scope

- Notification or email behavior
- New workflow or audit tables
- Global authorization policy replacement
- Broader role redesign scheduled for the RBAC sprint
- Changes to Cost Sheet status behavior
