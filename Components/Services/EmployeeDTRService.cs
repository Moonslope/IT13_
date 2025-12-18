using HestiaLink.Data;
using HestiaLink.Models;
using Microsoft.EntityFrameworkCore;

namespace HestiaLink.Services
{
    public class EmployeeDTRService
    {
        private readonly IDbContextFactory<HestiaLinkContext> _contextFactory;

        public EmployeeDTRService(IDbContextFactory<HestiaLinkContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Clock in for an employee (uses existing Attendance table linked to Schedule)
        /// </summary>
        public async Task<(bool Success, string Message, EmployeeAttendance? Attendance)> ClockInAsync(int employeeId, int? userId = null, string? location = null)
        {
            using var _context = _contextFactory.CreateDbContext();
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var now = DateTime.Now;
                
                // Find or create schedule for today
                var todaySchedule = await _context.Schedules
                    .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && 
                                             s.ScheduleDate == today && 
                                             s.IsActive == true);

                // If no schedule exists, create one for today (flexible schedule)
                if (todaySchedule == null)
                {
                    todaySchedule = new Schedule
                    {
                        EmployeeId = employeeId,
                        ScheduleDate = today,
                        ScheduledStart = TimeOnly.FromDateTime(now),
                        ScheduledEnd = TimeOnly.FromDateTime(now.AddHours(8)),
                        IsActive = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _context.Schedules.Add(todaySchedule);
                    await _context.SaveChangesAsync();
                }

                // Check if already clocked in today
                var existingAttendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.ScheduleId == todaySchedule.ScheduleId && 
                                             a.AttendanceDate == today &&
                                             a.ActualCheckIn != null &&
                                             a.ActualCheckOut == null);

                if (existingAttendance != null)
                {
                    return (false, "You are already clocked in for today. Please clock out first.", null);
                }

                // Get or create attendance record
                var attendanceRecord = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.ScheduleId == todaySchedule.ScheduleId && 
                                             a.AttendanceDate == today);

                if (attendanceRecord != null)
                {
                    attendanceRecord.ActualCheckIn = now;
                    attendanceRecord.AttendanceStatus = "Present";
                    attendanceRecord.UpdatedAt = now;
                    _context.Attendances.Update(attendanceRecord);
                }
                else
                {
                    attendanceRecord = new Attendance
                    {
                        ScheduleId = todaySchedule.ScheduleId,
                        AttendanceDate = today,
                        ActualCheckIn = now,
                        AttendanceStatus = "Present",
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _context.Attendances.Add(attendanceRecord);
                }

                await _context.SaveChangesAsync();
                
                // Return a mock EmployeeAttendance object for compatibility
                var mockAttendance = new EmployeeAttendance
                {
                    EmployeeAttendanceId = attendanceRecord.AttendanceId,
                    EmployeeId = employeeId,
                    UserId = userId,
                    AttendanceDate = today,
                    ClockInTime = now,
                    Status = "Present",
                    CreatedAt = now,
                    UpdatedAt = now
                };
                
                return (true, "Clock in successful", mockAttendance);
            }
            catch (Exception ex)
            {
                return (false, $"Error clocking in: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Clock out for an employee (uses existing Attendance table linked to Schedule)
        /// </summary>
        public async Task<(bool Success, string Message, EmployeeAttendance? Attendance)> ClockOutAsync(int employeeId, int? userId = null, string? location = null)
        {
            using var _context = _contextFactory.CreateDbContext();
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var now = DateTime.Now;
                
                // Find schedule for today
                var todaySchedule = await _context.Schedules
                    .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && 
                                             s.ScheduleDate == today && 
                                             s.IsActive == true);

                if (todaySchedule == null)
                {
                    return (false, "No schedule found for today. Please contact HR.", null);
                }

                // Find today's attendance record
                var attendanceRecord = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.ScheduleId == todaySchedule.ScheduleId && 
                                             a.AttendanceDate == today);

                if (attendanceRecord == null || attendanceRecord.ActualCheckIn == null)
                {
                    return (false, "You must clock in first before clocking out.", null);
                }

                if (attendanceRecord.ActualCheckOut != null)
                {
                    return (false, "You have already clocked out for today.", null);
                }

                attendanceRecord.ActualCheckOut = now;
                attendanceRecord.UpdatedAt = now;

                // Calculate hours worked
                var timeSpan = attendanceRecord.ActualCheckOut.Value - attendanceRecord.ActualCheckIn.Value;
                var totalHours = (decimal)timeSpan.TotalHours;
                
                // Assuming 8 hours is regular time, anything over is overtime
                var regularHours = totalHours > 8 ? 8 : totalHours;
                var overtimeHours = totalHours > 8 ? totalHours - 8 : 0;

                attendanceRecord.RegularHours = regularHours;
                attendanceRecord.OvertimeHours = overtimeHours;

                _context.Attendances.Update(attendanceRecord);
                await _context.SaveChangesAsync();
                
                // Return a mock EmployeeAttendance object for compatibility
                var mockAttendance = new EmployeeAttendance
                {
                    EmployeeAttendanceId = attendanceRecord.AttendanceId,
                    EmployeeId = employeeId,
                    UserId = userId,
                    AttendanceDate = today,
                    ClockInTime = attendanceRecord.ActualCheckIn,
                    ClockOutTime = now,
                    TotalHours = totalHours,
                    RegularHours = regularHours,
                    OvertimeHours = overtimeHours,
                    Status = "Present",
                    CreatedAt = attendanceRecord.CreatedAt ?? now,
                    UpdatedAt = now
                };
                
                return (true, "Clock out successful", mockAttendance);
            }
            catch (Exception ex)
            {
                return (false, $"Error clocking out: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Get today's attendance status for an employee (uses existing Attendance table)
        /// </summary>
        public async Task<EmployeeAttendance?> GetTodayAttendanceAsync(int employeeId)
        {
            using var _context = _contextFactory.CreateDbContext();
            var today = DateOnly.FromDateTime(DateTime.Now);
            
            // Find schedule for today
            var todaySchedule = await _context.Schedules
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.EmployeeId == employeeId && 
                                         s.ScheduleDate == today && 
                                         s.IsActive == true);

            if (todaySchedule == null)
            {
                return null;
            }

            // Find attendance record
            var attendanceRecord = await _context.Attendances
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ScheduleId == todaySchedule.ScheduleId && 
                                         a.AttendanceDate == today);

            if (attendanceRecord == null)
            {
                return null;
            }

            // Calculate total hours if both times exist
            decimal? totalHours = null;
            if (attendanceRecord.ActualCheckIn.HasValue && attendanceRecord.ActualCheckOut.HasValue)
            {
                totalHours = (decimal)(attendanceRecord.ActualCheckOut.Value - attendanceRecord.ActualCheckIn.Value).TotalHours;
            }
            else if (attendanceRecord.ActualCheckIn.HasValue)
            {
                totalHours = (decimal)(DateTime.Now - attendanceRecord.ActualCheckIn.Value).TotalHours;
            }

            // Convert to EmployeeAttendance format for compatibility
            return new EmployeeAttendance
            {
                EmployeeAttendanceId = attendanceRecord.AttendanceId,
                EmployeeId = employeeId,
                AttendanceDate = today,
                ClockInTime = attendanceRecord.ActualCheckIn,
                ClockOutTime = attendanceRecord.ActualCheckOut,
                TotalHours = totalHours ?? attendanceRecord.RegularHours + (attendanceRecord.OvertimeHours ?? 0),
                RegularHours = attendanceRecord.RegularHours,
                OvertimeHours = attendanceRecord.OvertimeHours,
                Status = attendanceRecord.AttendanceStatus ?? "Present",
                CreatedAt = attendanceRecord.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = attendanceRecord.UpdatedAt
            };
        }

        /// <summary>
        /// Get monthly attendance summary for an employee (uses existing Attendance table)
        /// </summary>
        public async Task<List<EmployeeAttendance>> GetMonthlyAttendanceAsync(int employeeId, int year, int month)
        {
            using var _context = _contextFactory.CreateDbContext();
            var startDate = new DateOnly(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            // Get schedules for the month
            var schedules = await _context.Schedules
                .AsNoTracking()
                .Where(s => s.EmployeeId == employeeId &&
                           s.ScheduleDate >= startDate &&
                           s.ScheduleDate <= endDate &&
                           s.IsActive == true)
                .ToListAsync();

            var scheduleIds = schedules.Select(s => s.ScheduleId).ToList();

            if (!scheduleIds.Any())
            {
                return new List<EmployeeAttendance>();
            }

            // Get attendance records for these schedules
            var attendanceRecords = await _context.Attendances
                .AsNoTracking()
                .Where(a => scheduleIds.Contains(a.ScheduleId) &&
                           a.AttendanceDate >= startDate &&
                           a.AttendanceDate <= endDate)
                .OrderByDescending(a => a.AttendanceDate)
                .ToListAsync();

            // Convert to EmployeeAttendance format
            return attendanceRecords.Select(a =>
            {
                var schedule = schedules.FirstOrDefault(s => s.ScheduleId == a.ScheduleId);
                decimal? totalHours = null;
                if (a.ActualCheckIn.HasValue && a.ActualCheckOut.HasValue)
                {
                    totalHours = (decimal)(a.ActualCheckOut.Value - a.ActualCheckIn.Value).TotalHours;
                }

                return new EmployeeAttendance
                {
                    EmployeeAttendanceId = a.AttendanceId,
                    EmployeeId = employeeId,
                    AttendanceDate = a.AttendanceDate,
                    ClockInTime = a.ActualCheckIn,
                    ClockOutTime = a.ActualCheckOut,
                    TotalHours = totalHours ?? (a.RegularHours ?? 0) + (a.OvertimeHours ?? 0),
                    RegularHours = a.RegularHours,
                    OvertimeHours = a.OvertimeHours,
                    Status = a.AttendanceStatus ?? "Present",
                    CreatedAt = a.CreatedAt ?? DateTime.UtcNow,
                    UpdatedAt = a.UpdatedAt
                };
            }).ToList();
        }

        /// <summary>
        /// Get attendance summary statistics for an employee (uses existing Attendance table)
        /// </summary>
        public async Task<(decimal TotalHours, decimal RegularHours, decimal OvertimeHours, int DaysPresent, int DaysAbsent)> GetAttendanceSummaryAsync(int employeeId, DateOnly startDate, DateOnly endDate)
        {
            using var _context = _contextFactory.CreateDbContext();
            
            // Get schedules for the period
            var schedules = await _context.Schedules
                .AsNoTracking()
                .Where(s => s.EmployeeId == employeeId &&
                           s.ScheduleDate >= startDate &&
                           s.ScheduleDate <= endDate &&
                           s.IsActive == true)
                .ToListAsync();

            var scheduleIds = schedules.Select(s => s.ScheduleId).ToList();

            if (!scheduleIds.Any())
            {
                return (0, 0, 0, 0, 0);
            }

            // Get attendance records
            var attendances = await _context.Attendances
                .AsNoTracking()
                .Where(a => scheduleIds.Contains(a.ScheduleId) &&
                           a.AttendanceDate >= startDate &&
                           a.AttendanceDate <= endDate)
                .ToListAsync();

            var totalHours = attendances.Sum(a => (a.RegularHours ?? 0) + (a.OvertimeHours ?? 0));
            var regularHours = attendances.Sum(a => a.RegularHours ?? 0);
            var overtimeHours = attendances.Sum(a => a.OvertimeHours ?? 0);
            var daysPresent = attendances.Count(a => a.AttendanceStatus == "Present" && a.ActualCheckIn != null);
            var daysAbsent = (endDate.DayNumber - startDate.DayNumber + 1) - daysPresent;

            return (totalHours, regularHours, overtimeHours, daysPresent, daysAbsent);
        }
    }
}

