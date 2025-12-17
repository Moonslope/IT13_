using System;
using System.Collections.Generic;
using HestiaLink.Models;
using HestiaLink.Models.Views;
using Microsoft.EntityFrameworkCore;

namespace HestiaLink.Data;

public partial class HestiaLinkContext : DbContext
{
    public HestiaLinkContext()
    {
    }

    public HestiaLinkContext(DbContextOptions<HestiaLinkContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Attendance> Attendances { get; set; }
    public virtual DbSet<Bill> Bills { get; set; }
    public virtual DbSet<BillDetail> BillDetails { get; set; }
    public virtual DbSet<Department> Departments { get; set; }
    public virtual DbSet<Employee> Employees { get; set; }
    public virtual DbSet<Guest> Guests { get; set; }
    public virtual DbSet<Income> Incomes { get; set; }
    public virtual DbSet<InventoryConsumption> InventoryConsumptions { get; set; }
    public virtual DbSet<InventoryItem> InventoryItems { get; set; }
    public virtual DbSet<OperationalExpense> OperationalExpenses { get; set; }
    public virtual DbSet<Payment> Payments { get; set; }
    public virtual DbSet<Payroll> Payrolls { get; set; }
    public virtual DbSet<Position> Positions { get; set; }
    public virtual DbSet<Reservation> Reservations { get; set; }
    public virtual DbSet<ReservedRoom> ReservedRooms { get; set; }
    public virtual DbSet<Room> Rooms { get; set; }
    public virtual DbSet<RoomType> RoomTypes { get; set; }
    public virtual DbSet<Schedule> Schedules { get; set; }
    public virtual DbSet<Service> Services { get; set; }
    public virtual DbSet<ServiceCategory> ServiceCategories { get; set; }
    public virtual DbSet<ServiceInventory> ServiceInventories { get; set; }
    public virtual DbSet<ServiceTransaction> ServiceTransactions { get; set; }
    public virtual DbSet<SystemUser> SystemUsers { get; set; }
    public virtual DbSet<Supplier> Suppliers { get; set; }
    public virtual DbSet<InventoryPurchase> InventoryPurchases { get; set; }
    public virtual DbSet<CleaningTask> CleaningTasks { get; set; }
    public virtual DbSet<Tax> Taxes { get; set; }
    public virtual DbSet<TotalIncome> TotalIncomes { get; set; }

    // Views
    public virtual DbSet<VwAttendanceDetail> VwAttendanceDetails { get; set; }
    public virtual DbSet<VwBookingHistory> VwBookingHistories { get; set; }
    public virtual DbSet<VwEmployeeFullDetail> VwEmployeeFullDetails { get; set; }
    public virtual DbSet<VwEmployeePayrollSummary> VwEmployeePayrollSummaries { get; set; }
    public virtual DbSet<VwRoomAvailability> VwRoomAvailabilities { get; set; }
    public virtual DbSet<VwRoomStatusDisplay> VwRoomStatusDisplays { get; set; }
    public virtual DbSet<VwRoomsBasic> VwRoomsBasics { get; set; }
    public virtual DbSet<VwRoomsNeedingCleaning> VwRoomsNeedingCleanings { get; set; }
    public virtual DbSet<VwTodaysRoomStatus> VwTodaysRoomStatuses { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Data Source=JESTER-PC\\SQLEXPRESS;Initial Catalog=IT13;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // OperationalExpense configuration
        modelBuilder.Entity<OperationalExpense>(entity =>
        {
            entity.HasKey(e => e.ExpenseId);
            entity.Property(e => e.ExpenseId).ValueGeneratedOnAdd();
            entity.ToTable("OperationalExpenses");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ExpenseDate).HasColumnType("datetime");
        });

        // Table mappings
        modelBuilder.Entity<Department>().ToTable("Department");
        modelBuilder.Entity<Position>().ToTable("Position");
        modelBuilder.Entity<Employee>().ToTable("Employee");
        modelBuilder.Entity<SystemUser>().ToTable("SystemUser");
        modelBuilder.Entity<Room>().ToTable("Room");
        modelBuilder.Entity<RoomType>().ToTable("RoomType");
        modelBuilder.Entity<ServiceCategory>().ToTable("ServiceCategory");
        modelBuilder.Entity<Service>().ToTable("Service");
        modelBuilder.Entity<Guest>().ToTable("Guest");
        modelBuilder.Entity<Reservation>().ToTable("Reservation");
        modelBuilder.Entity<ReservedRoom>().ToTable("ReservedRoom");
        modelBuilder.Entity<ServiceTransaction>().ToTable("ServiceTransaction");
        modelBuilder.Entity<Bill>().ToTable("Bill");
        modelBuilder.Entity<BillDetail>().ToTable("BillDetail");
        modelBuilder.Entity<Payment>().ToTable("Payment");
        modelBuilder.Entity<Attendance>().ToTable("Attendance");
        modelBuilder.Entity<Schedule>().ToTable("Schedule");
        modelBuilder.Entity<Tax>().ToTable("Tax");
        modelBuilder.Entity<Payroll>().ToTable("Payroll");
        modelBuilder.Entity<Income>().ToTable("Income");

        // TotalIncome configuration
        modelBuilder.Entity<TotalIncome>(entity =>
        {
            entity.ToTable("TotalIncome");
            entity.HasKey(e => e.IncomeId);
        });

        // Inventory tables
        modelBuilder.Entity<InventoryItem>().ToTable("InventoryItem");
        // ServiceInventory table mapping is configured below with relationships
        modelBuilder.Entity<InventoryConsumption>().ToTable("InventoryConsumption");
        modelBuilder.Entity<Supplier>().ToTable("Supplier");
        modelBuilder.Entity<InventoryPurchase>().ToTable("InventoryPurchase");

        // Housekeeping - Map to Task table
        modelBuilder.Entity<CleaningTask>().ToTable("Task");

        // Supplier configuration
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasIndex(s => s.SupplierCode).IsUnique();
        });

        // InventoryPurchase configuration
        modelBuilder.Entity<InventoryPurchase>(entity =>
        {
            entity.HasIndex(p => p.PurchaseNumber).IsUnique();

            entity.HasOne(p => p.InventoryItem)
                .WithMany()
                .HasForeignKey(p => p.ItemID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(p => p.Supplier)
                .WithMany(s => s.InventoryPurchases)
                .HasForeignKey(p => p.SupplierID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // InventoryItem-Supplier relationship
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasOne(i => i.Supplier)
                .WithMany(s => s.InventoryItems)
                .HasForeignKey(i => i.SupplierID)
                .OnDelete(DeleteBehavior.SetNull);

            // InventoryItem-ServiceCategory relationship
            entity.HasOne(i => i.ServiceCategory)
                .WithMany()
                .HasForeignKey(i => i.ServiceCategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure ServiceInventory Relationships
        modelBuilder.Entity<ServiceInventory>(entity =>
        {
            entity.ToTable("ServiceInventory");
            
            entity.Property(e => e.ServiceInventoryId)
                .HasColumnName("ServiceInventoryID");
            
            entity.Property(e => e.ServiceId)
                .HasColumnName("ServiceID");
            
            entity.Property(e => e.InventoryItemId)
                .HasColumnName("InventoryItemID");
            
            entity.HasOne(si => si.Service)
                .WithMany(s => s.ServiceInventories)
                .HasForeignKey(si => si.ServiceId)
                .HasConstraintName("FK_ServiceInventory_Service")
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(si => si.InventoryItem)
                .WithMany(i => i.ServiceInventories)
                .HasForeignKey(si => si.InventoryItemId)
                .HasConstraintName("FK_ServiceInventory_InventoryItem")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure CleaningTask (Task) Relationships
        modelBuilder.Entity<CleaningTask>(entity =>
        {
            entity.HasOne(ct => ct.Room)
                .WithMany(r => r.Tasks)
                .HasForeignKey(ct => ct.RoomID)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(ct => ct.AssignedUser)
                .WithMany()
                .HasForeignKey(ct => ct.UserID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure Views as keyless entities
        modelBuilder.Entity<VwAttendanceDetail>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_AttendanceDetail");
        });

        modelBuilder.Entity<VwBookingHistory>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_BookingHistory");
        });

        modelBuilder.Entity<VwEmployeeFullDetail>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_EmployeeFullDetail");
        });

        modelBuilder.Entity<VwEmployeePayrollSummary>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_EmployeePayrollSummary");
        });

        modelBuilder.Entity<VwRoomAvailability>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_RoomAvailability");
        });

        modelBuilder.Entity<VwRoomStatusDisplay>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_RoomStatusDisplay");
        });

        modelBuilder.Entity<VwRoomsBasic>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_RoomsBasic");
        });

        modelBuilder.Entity<VwRoomsNeedingCleaning>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_RoomsNeedingCleaning");
        });

        modelBuilder.Entity<VwTodaysRoomStatus>(entity =>
        {
            entity.HasNoKey();
            entity.ToView("vw_TodaysRoomStatus");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
