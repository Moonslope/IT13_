using HestiaLink.Data;
using HestiaLink.Models;
using Microsoft.EntityFrameworkCore;

namespace HestiaLink.Services
{
    public class PayrollBatchService
    {
        private readonly IDbContextFactory<HestiaLinkContext> _contextFactory;

        public PayrollBatchService(IDbContextFactory<HestiaLinkContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Create a new payroll batch for a period
        /// </summary>
        public async Task<(bool Success, string Message, PayrollBatch? Batch)> CreateBatchAsync(
            string batchName, 
            DateOnly periodStart, 
            DateOnly periodEnd, 
            List<int> employeeIds,
            decimal? activeTaxPercentage = null)
        {
            // PayrollBatch table doesn't exist - return error message
            return (false, "PayrollBatch table does not exist. Please create the database tables first.", null);
            
            /* Commented out - PayrollBatch table doesn't exist
            using var _context = _contextFactory.CreateDbContext();
            try
            {
                // Check if batch name already exists
                var existingBatch = await _context.PayrollBatches
                    .FirstOrDefaultAsync(b => b.BatchName == batchName);
                
                if (existingBatch != null)
                {
                    return (false, "A batch with this name already exists.", null);
                }

                // Get active employees
                var employees = await _context.Employees
                    .Include(e => e.Position)
                    .Where(e => employeeIds.Contains(e.EmployeeId) && e.Status == "Active")
                    .ToListAsync();

                if (!employees.Any())
                {
                    return (false, "No active employees found for the selected IDs.", null);
                }

                // Get tax percentage if not provided
                if (!activeTaxPercentage.HasValue)
                {
                    var activeTaxes = await _context.Taxes
                        .Where(t => t.Status == "Active")
                        .ToListAsync();
                    activeTaxPercentage = activeTaxes.Sum(t => t.TaxPercentage);
                }

                var batch = new PayrollBatch
                {
                    BatchName = batchName,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    Status = "Draft",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                _context.PayrollBatches.Add(batch);
                await _context.SaveChangesAsync();

                // Create payroll entries for each employee
                var entries = new List<PayrollEntry>();
                decimal totalBatchAmount = 0;

                foreach (var employee in employees)
                {
                    // Get attendance records for the period (using Attendance table linked to Schedule)
                    var schedules = await _context.Schedules
                        .Where(s => s.EmployeeId == employee.EmployeeId &&
                                   s.ScheduleDate >= periodStart &&
                                   s.ScheduleDate <= periodEnd &&
                                   s.IsActive == true)
                        .Select(s => s.ScheduleId)
                        .ToListAsync();

                    var attendances = await _context.Attendances
                        .Where(a => schedules.Contains(a.ScheduleId) &&
                                   a.AttendanceDate >= periodStart &&
                                   a.AttendanceDate <= periodEnd &&
                                   a.ActualCheckIn != null)
                        .ToListAsync();

                    var totalRegularHours = attendances.Sum(a => a.RegularHours ?? 0);
                    var totalOvertimeHours = attendances.Sum(a => a.OvertimeHours ?? 0);

                    // Determine hourly rate
                    decimal hourlyRate = 0;
                    decimal baseSalary = 0;
                    string salaryType = employee.SalaryType ?? "Hourly"; // Default to Hourly if not set
                    
                    // Note: HourlyRate, BaseSalary, SalaryType are currently [NotMapped] until database is updated
                    // For now, use Position.Salary as fallback
                    if (salaryType == "Hourly")
                    {
                        hourlyRate = employee.HourlyRate ?? employee.Position?.Salary ?? 0;
                    }
                    else if (salaryType == "Monthly")
                    {
                        baseSalary = employee.BaseSalary ?? employee.Position?.Salary ?? 0;
                        // Convert monthly to hourly (assuming 160 hours per month)
                        hourlyRate = baseSalary / 160m;
                    }
                    else
                    {
                        // Default: use Position.Salary as hourly rate
                        hourlyRate = employee.HourlyRate ?? employee.Position?.Salary ?? 0;
                    }

                    // Calculate pay
                    var regularPay = totalRegularHours * hourlyRate;
                    var overtimePay = totalOvertimeHours * (hourlyRate * 1.5m);
                    var grossPay = salaryType == "Monthly" ? baseSalary : (regularPay + overtimePay);
                    
                    // Apply tax
                    var taxAmount = grossPay * (activeTaxPercentage.Value / 100m);
                    var netPay = grossPay - taxAmount;

                    var entry = new PayrollEntry
                    {
                        PayrollBatchId = batch.PayrollBatchId,
                        EmployeeId = employee.EmployeeId,
                        RegularHours = totalRegularHours,
                        OvertimeHours = totalOvertimeHours,
                        HourlyRate = hourlyRate,
                        BaseSalary = salaryType == "Monthly" ? baseSalary : null,
                        RegularPay = employee.SalaryType == "Monthly" ? null : regularPay,
                        OvertimePay = employee.SalaryType == "Monthly" ? null : overtimePay,
                        GrossPay = grossPay,
                        TaxAmount = taxAmount,
                        NetPay = netPay,
                        Status = "Pending",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    entries.Add(entry);
                    totalBatchAmount += netPay;
                }

                _context.PayrollEntries.AddRange(entries);
                
                batch.TotalAmount = totalBatchAmount;
                batch.EntryCount = entries.Count;
                batch.UpdatedAt = DateTime.Now;

                // await _context.SaveChangesAsync(); // Commented - table doesn't exist

                // return (true, $"Batch created successfully with {entries.Count} entries.", batch);
            }
            catch (Exception ex)
            {
                return (false, $"Error creating batch: {ex.Message}", null);
            }
            */
        }

        /// <summary>
        /// Process a payroll batch (mark as paid)
        /// </summary>
        public async Task<(bool Success, string Message)> ProcessBatchAsync(int batchId, int userId, string paymentMethod = "Bank Transfer")
        {
            // PayrollBatch table doesn't exist - return error message
            return (false, "PayrollBatch table does not exist. Please create the database tables first.");
            
            /* Commented out - PayrollBatch table doesn't exist
            using var _context = _contextFactory.CreateDbContext();
            try
            {
                var batch = await _context.PayrollBatches
                    .Include(b => b.PayrollEntries)
                    .FirstOrDefaultAsync(b => b.PayrollBatchId == batchId);

                if (batch == null)
                {
                    return (false, "Batch not found.");
                }

                if (batch.Status == "Completed")
                {
                    return (false, "Batch has already been processed.");
                }

                batch.Status = "Processing";
                batch.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                // Update all entries to Paid
                foreach (var entry in batch.PayrollEntries)
                {
                    entry.Status = "Paid";
                    entry.UpdatedAt = DateTime.Now;
                }

                batch.Status = "Completed";
                batch.ProcessedDate = DateTime.Now;
                batch.ProcessedByUserId = userId;
                batch.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                // return (true, $"Batch processed successfully. {batch.PayrollEntries.Count} employees paid.");
            }
            catch (Exception ex)
            {
                return (false, $"Error processing batch: {ex.Message}");
            }
            */
        }

        /// <summary>
        /// Get all payroll batches
        /// </summary>
        public async Task<List<PayrollBatch>> GetAllBatchesAsync()
        {
            // PayrollBatch table doesn't exist - return empty list
            await Task.CompletedTask;
            return new List<PayrollBatch>();
            
            /* Commented out - PayrollBatch table doesn't exist
            using var _context = _contextFactory.CreateDbContext();
            return await _context.PayrollBatches
                .Include(b => b.PayrollEntries)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            */
        }

        /// <summary>
        /// Get batch with entries
        /// </summary>
        public async Task<PayrollBatch?> GetBatchWithEntriesAsync(int batchId)
        {
            // PayrollBatch table doesn't exist - return null
            await Task.CompletedTask;
            return null;
            
            /* Commented out - PayrollBatch table doesn't exist
            using var _context = _contextFactory.CreateDbContext();
            return await _context.PayrollBatches
                .Include(b => b.PayrollEntries)
                    .ThenInclude(e => e.Employee)
                        .ThenInclude(e => e.Position)
                .FirstOrDefaultAsync(b => b.PayrollBatchId == batchId);
            */
        }

        /// <summary>
        /// Delete a draft batch
        /// </summary>
        public async Task<(bool Success, string Message)> DeleteBatchAsync(int batchId)
        {
            // PayrollBatch table doesn't exist - return error message
            await Task.CompletedTask;
            return (false, "PayrollBatch table does not exist. Please create the database tables first.");
            
            /* Commented out - PayrollBatch table doesn't exist
            using var _context = _contextFactory.CreateDbContext();
            try
            {
                var batch = await _context.PayrollBatches
                    .Include(b => b.PayrollEntries)
                    .FirstOrDefaultAsync(b => b.PayrollBatchId == batchId);

                if (batch == null)
                {
                    return (false, "Batch not found.");
                }

                if (batch.Status != "Draft")
                {
                    return (false, "Only draft batches can be deleted.");
                }

                // _context.PayrollEntries.RemoveRange(batch.PayrollEntries); // Commented - table doesn't exist
                // _context.PayrollBatches.Remove(batch); // Commented - table doesn't exist
                // await _context.SaveChangesAsync(); // Commented - table doesn't exist

                // return (true, "Batch deleted successfully.");
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting batch: {ex.Message}");
            }
            */
        }
    }
}

