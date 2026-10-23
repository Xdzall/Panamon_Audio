using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace MonitoringSystem.ModelsMachine;

public partial class ScaffoldedMachineDbContext : DbContext
{
    public ScaffoldedMachineDbContext()
    {
    }

    public ScaffoldedMachineDbContext(DbContextOptions<ScaffoldedMachineDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Efficiency> Efficiencies { get; set; }

    public virtual DbSet<EfficiencyLoss> EfficiencyLosses { get; set; }

    public virtual DbSet<MachineList> MachineLists { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=AUDIO_PRODUCTION;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Efficiency>(entity =>
        {
            entity.ToTable("Efficiency");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.Ability).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Achievement).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DefectQty).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.GoodProductionQty).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Oee)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("OEE");
            entity.Property(e => e.OperatingRatio).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.PlanQty).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.Quality).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Shift).HasMaxLength(20);
            entity.Property(e => e.WorkingTime).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.IdMachineNavigation).WithMany(p => p.Efficiencies)
                .HasForeignKey(d => d.IdMachine)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Efficiency_MachineList");
        });

        modelBuilder.Entity<EfficiencyLoss>(entity =>
        {
            entity.ToTable("EfficiencyLoss");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.EfficiencyId).HasColumnName("EfficiencyID");
            entity.Property(e => e.LossCategory).HasMaxLength(100);
            entity.Property(e => e.LossGroup).HasMaxLength(50);
            entity.Property(e => e.LossMinutes).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Efficiency).WithMany(p => p.EfficiencyLosses)
                .HasForeignKey(d => d.EfficiencyId)
                .HasConstraintName("FK_EfficiencyLoss_Efficiency");
        });

        modelBuilder.Entity<MachineList>(entity =>
        {
            entity.HasKey(e => e.IdMachine);

            entity.ToTable("MachineList");

            entity.Property(e => e.MachineName).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
