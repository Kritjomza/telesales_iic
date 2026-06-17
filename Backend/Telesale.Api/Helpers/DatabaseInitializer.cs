using System;
using System.Threading.Tasks;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;

namespace Telesale.Api.Helpers;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(TelesaleDbContext db)
    {
        var createSessionTableSql = @"
            CREATE TABLE IF NOT EXISTS `import_sessions` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `imported_by` INT UNSIGNED NOT NULL,
                `file_name` VARCHAR(255) NULL,
                `total_rows` INT NOT NULL,
                `imported_rows` INT NOT NULL,
                `skipped_rows` INT NOT NULL,
                `error_rows` INT NOT NULL,
                `errors_json` LONGTEXT NULL,
                `created_at` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;
        ";

        var createRowTableSql = @"
            CREATE TABLE IF NOT EXISTS `import_rows` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `session_id` INT UNSIGNED NOT NULL,
                `row_data_json` LONGTEXT NOT NULL,
                `status` VARCHAR(50) NOT NULL,
                `error_message` TEXT NULL,
                `created_at` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;
        ";

        var createAssignmentHistoryTableSql = @"
            CREATE TABLE IF NOT EXISTS `assignment_history` (
                `id` INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
                `customer_id` INT UNSIGNED NOT NULL,
                `old_sale_id` INT NULL,
                `new_sale_id` INT NULL,
                `old_telesale_id` INT NULL,
                `new_telesale_id` INT NULL,
                `changed_by_id` INT UNSIGNED NULL,
                `changed_at` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,
                `reason` VARCHAR(255) NULL,
                FOREIGN KEY (`customer_id`) REFERENCES `customer` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;
        ";

        await db.Database.ExecuteSqlRawAsync(createSessionTableSql);
        await db.Database.ExecuteSqlRawAsync(createRowTableSql);
        await db.Database.ExecuteSqlRawAsync(createAssignmentHistoryTableSql);

        await EnsureTableColumnAsync(db, "customer", "subdistrict", "VARCHAR(255) NULL");
        await EnsureTableColumnAsync(db, "customer", "district", "VARCHAR(255) NULL");
        await EnsureTableColumnAsync(db, "customer", "province", "VARCHAR(255) NULL");
        await EnsureTableColumnAsync(db, "customer", "postal_code", "VARCHAR(10) NULL");
        await EnsureTableColumnAsync(db, "customer", "user_cnt", "INT NULL");
        await EnsureTableColumnAsync(db, "detail_device", "purchase_date", "DATE NULL");

        await EnsureCategoryExistsAsync(db, "Switch");
        await EnsureCategoryExistsAsync(db, "Access Point");
        await EnsureCategoryExistsAsync(db, "Server");
        await EnsureCategoryExistsAsync(db, "Storage");
        await EnsureCategoryExistsAsync(db, "Antivirus");
        await EnsureCategoryExistsAsync(db, "Software");

        await MigrateProjectStatusesAsync(db);
    }

    private static async Task EnsureTableColumnAsync(
        TelesaleDbContext db,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = @"
                SELECT COUNT(*)
                FROM information_schema.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = @tableName
                  AND COLUMN_NAME = @columnName;";

            var paramTable = checkCommand.CreateParameter();
            paramTable.ParameterName = "@tableName";
            paramTable.Value = tableName;
            checkCommand.Parameters.Add(paramTable);

            var paramColumn = checkCommand.CreateParameter();
            paramColumn.ParameterName = "@columnName";
            paramColumn.Value = columnName;
            checkCommand.Parameters.Add(paramColumn);

            var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
            if (exists)
            {
                return;
            }

            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDefinition};";
            await alterCommand.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureCategoryExistsAsync(TelesaleDbContext db, string name)
    {
        var exists = await db.categories.AnyAsync(c => c.name == name);
        if (!exists)
        {
            db.categories.Add(new category
            {
                name = name,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task MigrateProjectStatusesAsync(TelesaleDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            // 1. Get column type of progress_status in detail_pj
            await using var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = @"
                SELECT COLUMN_TYPE 
                FROM information_schema.COLUMNS 
                WHERE TABLE_SCHEMA = DATABASE() 
                  AND TABLE_NAME = 'detail_pj' 
                  AND COLUMN_NAME = 'progress_status';";
            
            var columnType = await checkCmd.ExecuteScalarAsync() as string;
            
            // 2. If it is an enum, alter it to VARCHAR(255)
            if (columnType != null && columnType.StartsWith("enum", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Migrating detail_pj.progress_status from ENUM to VARCHAR(255)...");
                await using var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE `detail_pj` MODIFY COLUMN `progress_status` VARCHAR(255) NOT NULL DEFAULT 'Discuss';";
                await alterCmd.ExecuteNonQueryAsync();
            }

            // 3. Update legacy values and null/unsupported values
            await using var updateNullCmd = connection.CreateCommand();
            updateNullCmd.CommandText = "UPDATE `detail_pj` SET `progress_status` = 'Discuss' WHERE `progress_status` IS NULL;";
            await updateNullCmd.ExecuteNonQueryAsync();

            await using var updateDiscussCmd = connection.CreateCommand();
            updateDiscussCmd.CommandText = "UPDATE `detail_pj` SET `progress_status` = 'Discuss' WHERE `progress_status` = 'Disscuss';";
            await updateDiscussCmd.ExecuteNonQueryAsync();

            await using var updateQuotationCmd = connection.CreateCommand();
            updateQuotationCmd.CommandText = "UPDATE `detail_pj` SET `progress_status` = 'Quotation' WHERE `progress_status` = 'Quatation';";
            await updateQuotationCmd.ExecuteNonQueryAsync();

            await using var updateOtherCmd = connection.CreateCommand();
            updateOtherCmd.CommandText = "UPDATE `detail_pj` SET `progress_status` = 'Discuss' WHERE `progress_status` NOT IN ('Discuss', 'Quotation', 'Win', 'Lost', 'Hold', 'Cancel');";
            int affectedRows = await updateOtherCmd.ExecuteNonQueryAsync();
            if (affectedRows > 0)
            {
                Console.WriteLine($"Migrated {affectedRows} legacy project status values.");
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
