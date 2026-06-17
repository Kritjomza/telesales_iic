using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Models;

namespace Telesale.Api.Data;

public partial class TelesaleDbContext : DbContext
{
    public TelesaleDbContext(DbContextOptions<TelesaleDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<antivirus_price_list> antivirus_price_lists { get; set; }

    public virtual DbSet<antivirus_service_list> antivirus_service_lists { get; set; }

    public virtual DbSet<brand> brands { get; set; }

    public virtual DbSet<business_type> business_types { get; set; }

    public virtual DbSet<category> categories { get; set; }

    public virtual DbSet<competitor> competitors { get; set; }

    public virtual DbSet<cost_sheet> cost_sheets { get; set; }

    public virtual DbSet<customer> customers { get; set; }

    public virtual DbSet<detail> details { get; set; }

    public virtual DbSet<detail_device> detail_devices { get; set; }

    public virtual DbSet<detail_pj> detail_pjs { get; set; }

    public virtual DbSet<log> logs { get; set; }

    public virtual DbSet<migration> migrations { get; set; }

    public virtual DbSet<password_reset> password_resets { get; set; }

    public virtual DbSet<product> products { get; set; }

    public virtual DbSet<profile> profiles { get; set; }

    public virtual DbSet<target> targets { get; set; }

    public virtual DbSet<user> users { get; set; }

    public virtual DbSet<import_session> import_sessions { get; set; }

    public virtual DbSet<import_row> import_rows { get; set; }

    public virtual DbSet<assignment_history> assignment_histories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("latin1_swedish_ci")
            .HasCharSet("latin1");

        modelBuilder.Entity<antivirus_price_list>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("antivirus_price_list")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.brand).HasMaxLength(255);
            entity.Property(e => e.code).HasMaxLength(255);
            entity.Property(e => e.cost).HasColumnType("double(14,2)");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.edition).HasMaxLength(255);
            entity.Property(e => e.end).HasColumnType("int(11)");
            entity.Property(e => e.start).HasColumnType("int(11)");
            entity.Property(e => e.types)
                .HasDefaultValueSql("'Client'")
                .HasColumnType("enum('Client','Server')");
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<antivirus_service_list>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("antivirus_service_list")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.detail).HasMaxLength(255);
            entity.Property(e => e.margin).HasColumnType("int(11)");
            entity.Property(e => e.name).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<brand>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.name).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<business_type>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("business_type")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.dtl).HasMaxLength(255);
            entity.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.type).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<category>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.name).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<competitor>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("competitor")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.amt).HasColumnType("double(15,2)");
            entity.Property(e => e.compare).HasColumnType("enum('Bigger','Smaller')");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.name).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
            entity.Property(e => e.year)
                .HasMaxLength(4)
                .IsFixedLength();
        });

        modelBuilder.Entity<cost_sheet>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("cost_sheet")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.address).HasMaxLength(255);
            entity.Property(e => e.attention).HasMaxLength(255);
            entity.Property(e => e.brand).HasMaxLength(255);
            entity.Property(e => e.company).HasMaxLength(255);
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.desktop_qty).HasColumnType("int(11)");
            entity.Property(e => e.edition).HasMaxLength(255);
            entity.Property(e => e.email).HasMaxLength(255);
            entity.Property(e => e.fax).HasMaxLength(255);
            entity.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.margin).HasColumnType("int(11)");
            entity.Property(e => e.qo_no).HasMaxLength(255);
            entity.Property(e => e.revised_no).HasMaxLength(255);
            entity.Property(e => e.server_qty).HasColumnType("int(11)");
            entity.Property(e => e.status)
                .HasMaxLength(255)
                .HasDefaultValueSql("'NEW'");
            entity.Property(e => e.tel).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<customer>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("customer")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.address).HasMaxLength(255);
            entity.Property(e => e.business_type_id).HasColumnType("int(11)");
            entity.Property(e => e.capital).HasColumnType("double(14,2)");
            entity.Property(e => e.code).HasMaxLength(255);
            entity.Property(e => e.create_type)
                .HasDefaultValueSql("'Key'")
                .HasColumnType("enum('Key','Import')");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.name).HasMaxLength(255);
            entity.Property(e => e.owner_id).HasColumnType("int(11)");
            entity.Property(e => e.phone).HasMaxLength(255);
            entity.Property(e => e.sale_id).HasColumnType("int(11)");
            entity.Property(e => e.sale_id_bak).HasColumnType("int(11)");
            entity.Property(e => e.status)
                .HasMaxLength(255)
                .HasDefaultValueSql("'New'");
            entity.Property(e => e.telesale_id).HasColumnType("int(11)");
            entity.Property(e => e.telesale_id_bak).HasColumnType("int(11)");
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
            entity.Property(e => e.updated_user).HasColumnType("int(11)");
            entity.Property(e => e.subdistrict).HasMaxLength(255);
            entity.Property(e => e.district).HasMaxLength(255);
            entity.Property(e => e.province).HasMaxLength(255);
            entity.Property(e => e.postal_code).HasMaxLength(10);
            entity.Property(e => e.user_cnt).HasColumnType("int(11)");
        });

        modelBuilder.Entity<detail>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("detail")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.HasIndex(e => e.cust_id, "detail_cust_id_foreign");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.bak_point).HasColumnType("int(11)");
            entity.Property(e => e.contact_email).HasMaxLength(255);
            entity.Property(e => e.contact_name).HasMaxLength(255);
            entity.Property(e => e.contact_position).HasMaxLength(255);
            entity.Property(e => e.contact_tel).HasMaxLength(255);
            entity.Property(e => e.contact_tel_office).HasMaxLength(255);
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.cust_id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.point).HasColumnType("int(11)");
            entity.Property(e => e.profit_last_year).HasColumnType("int(11)");
            entity.Property(e => e.total_point).HasColumnType("int(11)");
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
            entity.Property(e => e.user_cnt).HasColumnType("int(11)");

            entity.HasOne(d => d.cust).WithMany(p => p.details)
                .HasForeignKey(d => d.cust_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detail_cust_id_foreign");
        });

        modelBuilder.Entity<detail_device>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("detail_device")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.HasIndex(e => e.dtl_id, "detail_device_dtl_id_foreign");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.bak_point).HasColumnType("int(11)");
            entity.Property(e => e.brand_id).HasColumnType("int(11)");
            entity.Property(e => e.category_id).HasColumnType("int(11)");
            entity.Property(e => e.competitor_id).HasColumnType("int(11)");
            entity.Property(e => e.cost_sheet).HasColumnType("int(11)");
            entity.Property(e => e.count_renewal).HasColumnType("int(11)");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.desktop_qty).HasColumnType("int(11)");
            entity.Property(e => e.dtl_dv_id).HasColumnType("int(11)");
            entity.Property(e => e.dtl_id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.equipment_dtl).HasMaxLength(255);
            entity.Property(e => e.equipment_id).HasColumnType("int(11)");
            entity.Property(e => e.equipment_qty).HasColumnType("int(11)");
            entity.Property(e => e.point).HasColumnType("int(11)");
            entity.Property(e => e.progress_status).HasMaxLength(255);
            entity.Property(e => e.server_qty).HasColumnType("int(11)");
            entity.Property(e => e.updated_at).HasColumnType("timestamp");

            entity.HasOne(d => d.dtl).WithMany(p => p.detail_devices)
                .HasForeignKey(d => d.dtl_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detail_device_dtl_id_foreign");
        });

        modelBuilder.Entity<detail_pj>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("detail_pj")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.HasIndex(e => e.dtl_id, "detail_pj_dtl_id_foreign");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.amt).HasColumnType("double(14,2)");
            entity.Property(e => e.bak_point).HasColumnType("int(11)");
            entity.Property(e => e.competitor_id).HasColumnType("int(11)");
            entity.Property(e => e.contact).HasMaxLength(255);
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.dtl).HasMaxLength(255);
            entity.Property(e => e.dtl_id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.point).HasColumnType("int(11)");
            entity.Property(e => e.profit).HasColumnType("double(14,2)");
            entity.Property(e => e.progress_status)
                .HasDefaultValueSql("'Disscuss'")
                .HasColumnType("enum('Disscuss','Quatation','Win','Lost','Hold','Cancel')");
            entity.Property(e => e.reason).HasMaxLength(255);
            entity.Property(e => e.tel).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");

            entity.HasOne(d => d.dtlNavigation).WithMany(p => p.detail_pjs)
                .HasForeignKey(d => d.dtl_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("detail_pj_dtl_id_foreign");
        });

        modelBuilder.Entity<log>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("log")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.dtl).HasMaxLength(255);
            entity.Property(e => e.type).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<migration>(entity =>
        {
            entity
                .HasNoKey()
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.batch).HasColumnType("int(11)");
            entity.Property(e => e.migration1)
                .HasMaxLength(255)
                .HasColumnName("migration");
        });

        modelBuilder.Entity<password_reset>(entity =>
        {
            entity
                .HasNoKey()
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.HasIndex(e => e.email, "password_resets_email_index");

            entity.HasIndex(e => e.token, "password_resets_token_index");

            entity.Property(e => e.created_at)
                .ValueGeneratedOnAddOrUpdate()
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp");
        });

        modelBuilder.Entity<product>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.HasIndex(e => e.brands_id, "products_brands_id_foreign");

            entity.HasIndex(e => e.categories_id, "products_categories_id_foreign");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.brands_id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.categories_id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.name).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");

            entity.HasOne(d => d.brands).WithMany(p => p.products)
                .HasForeignKey(d => d.brands_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("products_brands_id_foreign");

            entity.HasOne(d => d.categories).WithMany(p => p.products)
                .HasForeignKey(d => d.categories_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("products_categories_id_foreign");
        });

        modelBuilder.Entity<profile>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("profile")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.name).HasMaxLength(255);
            entity.Property(e => e.type)
                .HasMaxLength(255)
                .HasDefaultValueSql("'ANTIVIRUS'");
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<target>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("target")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.point).HasColumnType("int(11)");
            entity.Property(e => e.target_type).HasColumnType("enum('CC','PK','QP','PS','QR','RS')");
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
            entity.Property(e => e.user_id).HasColumnType("int(11)");
        });

        modelBuilder.Entity<user>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.HasIndex(e => e.email, "users_email_unique").IsUnique();

            entity.HasIndex(e => e.username, "users_username_unique").IsUnique();

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.is_active)
                .IsRequired()
                .HasDefaultValueSql("'1'");
            entity.Property(e => e.failed_login_count)
                .HasColumnType("int(11)")
                .HasDefaultValueSql("'0'");
            entity.Property(e => e.locked_until).HasColumnType("datetime");
            entity.Property(e => e.last_login_at).HasColumnType("datetime");
            entity.Property(e => e.linetoken).HasMaxLength(255);
            entity.Property(e => e.name).HasMaxLength(255);
            entity.Property(e => e.password).HasMaxLength(255);
            entity.Property(e => e.position).HasMaxLength(255);
            entity.Property(e => e.remember_token).HasMaxLength(100);
            entity.Property(e => e.roles)
                .HasDefaultValueSql("'Super Admin'")
                .HasColumnType("enum('Admin','Super Admin','Manager','Tele Sale','Sale')");
            entity.Property(e => e.tel).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<import_session>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("import_sessions")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.imported_by).HasColumnType("int(10) unsigned");
            entity.Property(e => e.file_name).HasMaxLength(255);
            entity.Property(e => e.total_rows).HasColumnType("int(11)");
            entity.Property(e => e.imported_rows).HasColumnType("int(11)");
            entity.Property(e => e.skipped_rows).HasColumnType("int(11)");
            entity.Property(e => e.error_rows).HasColumnType("int(11)");
            entity.Property(e => e.errors_json).HasColumnType("longtext");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
            entity.Property(e => e.updated_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<import_row>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("import_rows")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.session_id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.row_data_json).HasColumnType("longtext");
            entity.Property(e => e.status).HasMaxLength(50);
            entity.Property(e => e.error_message).HasColumnType("text");
            entity.Property(e => e.created_at).HasColumnType("timestamp");
        });

        modelBuilder.Entity<assignment_history>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PRIMARY");

            entity
                .ToTable("assignment_history")
                .HasCharSet("utf8")
                .UseCollation("utf8_unicode_ci");

            entity.HasIndex(e => e.customer_id, "assignment_history_customer_id_foreign");

            entity.Property(e => e.id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.customer_id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.old_sale_id).HasColumnType("int(11)");
            entity.Property(e => e.new_sale_id).HasColumnType("int(11)");
            entity.Property(e => e.old_telesale_id).HasColumnType("int(11)");
            entity.Property(e => e.new_telesale_id).HasColumnType("int(11)");
            entity.Property(e => e.changed_by_id).HasColumnType("int(10) unsigned");
            entity.Property(e => e.changed_at).HasColumnType("timestamp");
            entity.Property(e => e.reason).HasMaxLength(255);

            entity.HasOne(d => d.customer).WithMany()
                .HasForeignKey(d => d.customer_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("assignment_history_customer_id_foreign");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
