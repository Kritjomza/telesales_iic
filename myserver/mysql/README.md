# MySQL compatibility notes

The source system uses MySQL 5.7 and the database name `sale`. Its schema and data include mixed legacy charset and collation behavior.

- Restore a full logical backup. Do not copy the legacy `mysql/data` directory.
- Do not automatically convert `latin1`, `utf8`, `utf8mb4`, or collations during restore.
- Do not assume EF Core models can recreate the full legacy schema.
- The custom ASP.NET Core `DatabaseInitializer` is not a complete schema installer.
- Do not run Laravel migrations/seeds or EF migrations for a full-dump restore.

MySQL 5.7 is end-of-life. The `MYSQL_IMAGE=mysql:5.7` default is a temporary compatibility measure, not an endorsement for long-term operation. A MySQL 8 upgrade must be a separate project with tested backup and rollback procedures.

Before any upgrade, inventory and test:

- removed SQL modes and features;
- newly reserved words and application SQL;
- signed and unsigned column behavior;
- enum values and comparisons;
- timestamp defaults and zero dates;
- indexes, key lengths, and query plans;
- Thai text round trips;
- collation-sensitive search and sorting;
- triggers, routines, and events;
- every application query and import/report path.

No automatic schema or charset conversion belongs in this deployment.
