using HestiaLink.Data;
using HestiaLink.Models;
using Microsoft.EntityFrameworkCore;

namespace HestiaLink.Services
{
    public class AttendanceService
    {
        private readonly HestiaLinkContext _context;

        public AttendanceService(HestiaLinkContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gets all active employees with their schedules and attendance for a specific date
        /// Uses Employee -> Schedule -> Attendance flow
        /// </summary>
        public async Task<List<EmployeeWithScheduleDto>> GetAllEmployeesForDateAsync(DateTime date)
        {
            try
            {
                // Get ALL active employees
                var allEmployees = await _context.Employees
                    .Where(e => e.Status == "Active")
                    .Include(e => e.Position)
                    .ThenInclude(p => p!.Department)
                    .AsNoTracking()
                    .ToListAsync();

                // Get schedules for this date (keyed by EmployeeId)
                var schedules = await _context.Schedules
                    .Where(s => s.ScheduleDate == DateOnly.FromDateTime(date) && s.IsActive == true)
                    .AsNoTracking()
                    .ToDictionaryAsync(s => s.EmployeeId);

                // Get schedule IDs for the date
                var ScheduleIds = schedules.Values.Select(s => s.ScheduleId).ToList();

                // Get attendance via ScheduleId (not EmployeeId)
                var attendances = await _context.Attendances
                    .Where(a => a.AttendanceDate == DateOnly.FromDateTime(date) && ScheduleIds.Contains(a.ScheduleId))
                    .AsNoTracking()
                    .ToDictionaryAsync(a => a.ScheduleId);

                var result = new List<EmployeeWithScheduleDto>();

                foreach (var emp in allEmployees)
                {
                    var position = emp.Position;
                    var department = position?.Department;
                    schedules.TryGetValue(emp.EmployeeId, out var schedule);
                    
                    // Get attendance through schedule
                    Attendance? attendance = null;
                    if (schedule != null)
                    {
                        attendances.TryGetValue(schedule.ScheduleId, out attendance);
                    }

                    // Calculate IsLate dynamically
                    var isLate = false;
                    if (attendance?.ActualCheckIn != null && schedule != null)
                    {
                        var graceTime = schedule.ScheduledStart.AddMinutes(15);
                        isLate = TimeOnly.FromTimeSpan(attendance.ActualCheckIn.Value.TimeOfDay) > graceTime;
                    }

                    var dto = new EmployeeWithScheduleDto
                    {
                        EmployeeId = emp.EmployeeId,
                        FirstName = emp.FirstName,
                        LastName = emp.LastName,
                        Department = department?.DepartmentName ?? "-",
                        Position = position?.PositionTitle ?? "-",
                        AttendanceDate = date,
                        ScheduleId = schedule?.ScheduleId,
                        ScheduleStart = schedule?.ScheduledStart.ToTimeSpan(),
                        ScheduleEnd = schedule?.ScheduledEnd.ToTimeSpan(),
                        AttendanceId = attendance?.AttendanceId,
                        ActualCheckIn = attendance?.ActualCheckIn,
                        ActualCheckOut = attendance?.ActualCheckOut,
                        AttendanceStatus = attendance?.AttendanceStatus ?? (schedule != null ? "Pending" : "Not Scheduled"),
                        IsLate = isLate,
                        RegularHours = attendance?.RegularHours ?? 0,
                        OvertimeHours = attendance?.OvertimeHours ?? 0,
                        Notes = attendance?.Notes
                    };

                    result.Add(dto);
                }

                return result.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting employees for date: {ex.Message}");
                return new List<EmployeeWithScheduleDto>();
            }
        }

        /// <summary>
        /// Gets attendance records for a specific date via Schedule connection
        /// </summary>
        public async Task<List<AttendanceRecordDto>> GetAttendanceByDateAsync(DateTime attendanceDate, int? departmentId = null)
        {
            try
            {
                // Get schedules for the date
                var schedules = await _context.Schedules
                    .Where(s => s.ScheduleDate == DateOnly.FromDateTime(attendanceDate) && s.IsActive == true)
                    .AsNoTracking()
                    .ToListAsync();

                var ScheduleIds = schedules.Select(s => s.ScheduleId).ToList();

                // Get attendance records via ScheduleId
                var records = await _context.Attendances
                    .Where(a => a.AttendanceDate == DateOnly.FromDateTime(attendanceDate) && ScheduleIds.Contains(a.ScheduleId))
                    .Include(a => a.Schedule)
                    .AsNoTracking()
                    .ToListAsync();

                // Get employee IDs from schedules
                var EmployeeIds = schedules.Select(s => s.EmployeeId).Distinct().ToList();

                var employees = await _context.Employees
                    .Where(e => EmployeeIds.Contains(e.EmployeeId))
                    .Include(e => e.Position)
                    .ThenInclude(p => p!.Department)
                    .AsNoTracking()
                    .ToDictionaryAsync(e => e.EmployeeId);

                // Create schedule lookup by ID
                var scheduleLookup = schedules.ToDictionary(s => s.ScheduleId);

                var attendanceList = new List<AttendanceRecordDto>();

                foreach (var record in records)
                {
                    // Get employee through schedule
                    if (!scheduleLookup.TryGetValue(record.ScheduleId, out var schedule))
                        continue;

                    if (!employees.TryGetValue(schedule.EmployeeId, out var employee))
                        continue;

                    var position = employee.Position;
                    var department = position?.Department;

                    if (departmentId.HasValue && position?.DepartmentId != departmentId)
                        continue;

                    // Calculate IsLate dynamically
                    var isLate = false;
                    if (record.ActualCheckIn != null && schedule != null)
                    {
                        var graceTime = schedule.ScheduledStart.AddMinutes(15);
                        isLate = TimeOnly.FromTimeSpan(record.ActualCheckIn.Value.TimeOfDay) > graceTime;
                    }

                    attendanceList.Add(new AttendanceRecordDto
                    {
                        AttendanceId = record.AttendanceId,
                        EmployeeId = schedule?.EmployeeId ?? 0,
                        FirstName = employee?.FirstName ?? "",
                        LastName = employee?.LastName ?? "",
                        Department = department?.DepartmentName ?? "-",
                        Position = position?.PositionTitle ?? "-",
                        AttendanceDate = record.AttendanceDate.ToDateTime(TimeOnly.MinValue),
                        ScheduleStart = schedule?.ScheduledStart.ToTimeSpan() ?? TimeSpan.Zero,
                        ScheduleEnd = schedule?.ScheduledEnd.ToTimeSpan() ?? TimeSpan.Zero,
                        ActualCheckIn = record.ActualCheckIn,
                        ActualCheckOut = record.ActualCheckOut,
                        RegularHours = record.RegularHours ?? 0,
                        OvertimeHours = record.OvertimeHours ?? 0,
                        AttendanceStatus = record.AttendanceStatus ?? "Present",
                        IsLate = isLate,
                        Notes = record.Notes,
                        HoursWorked = record.ActualCheckIn.HasValue && record.ActualCheckOut.HasValue
                            ? (decimal)(record.ActualCheckOut.Value - record.ActualCheckIn.Value).TotalHours
                            : 0
                    });
                }

                return attendanceList.OrderBy(a => a.FirstName).ThenBy(a => a.LastName).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting attendance records: {ex.Message}");
                return new List<AttendanceRecordDto>();
            }
        }

        /// <summary>
        /// Records employee check-in via Schedule connection
        /// </summary>
        public async Task<bool> RecordCheckInAsync(int EmployeeId, DateTime attendanceDate, int? ScheduleId)
        {
            try
            {
                if (!ScheduleId.HasValue)
                {
                    Console.WriteLine("Cannot check in without a schedule");
                    return false;
                }

                // Check if attendance already exists for this schedule
                var attendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.ScheduleId == ScheduleId.Value);

                var checkInTime = DateTime.Now;

                if (attendance == null)
                {
                    var newAttendance = new Attendance
                    {
                        ScheduleId = ScheduleId.Value,
                        AttendanceDate = DateOnly.FromDateTime(attendanceDate),
                        ActualCheckIn = checkInTime,
                        AttendanceStatus = "Present",
                        RegularHours = 0,
                        OvertimeHours = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.Attendances.Add(newAttendance);
                }
                else
                {
                    attendance.ActualCheckIn = checkInTime;
                    attendance.AttendanceStatus = "Present";
                    attendance.UpdatedAt = DateTime.UtcNow;
                    _context.Attendances.Update(attendance);
                }

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recording check-in: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Records employee check-out via Schedule connection
        /// </summary>
        public async Task<bool> RecordCheckOutAsync(int EmployeeId, DateTime attendanceDate)
        {
            try
            {
                // Find schedule for employee on this date
                var schedule = await _context.Schedules
                    .FirstOrDefaultAsync(s => s.EmployeeId == EmployeeId && s.ScheduleDate == DateOnly.FromDateTime(attendanceDate) && s.IsActive == true);

                if (schedule == null)
                {
                    Console.WriteLine("No schedule found for employee");
                    return false;
                }

                // Find attendance via schedule
                var attendance = await _context.Attendances
                    .FirstOrDefaultAsync(a => a.ScheduleId == schedule.ScheduleId);

                if (attendance == null || !attendance.ActualCheckIn.HasValue)
                {
                    return false;
                }

                var checkOutTime = DateTime.Now;
                var totalHours = (decimal)(checkOutTime - attendance.ActualCheckIn.Value).TotalHours;

                if (totalHours <= 8)
                {
                    attendance.RegularHours = totalHours;
                    attendance.OvertimeHours = 0;
                }
                else
                {
                    attendance.RegularHours = 8;
                    attendance.OvertimeHours = totalHours - 8;
                }

                attendance.ActualCheckOut = checkOutTime;
                attendance.UpdatedAt = DateTime.UtcNow;
                _context.Attendances.Update(attendance);

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recording check-out: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets attendance history for an employee via Schedule connection
        /// </summary>
        public async Task<List<AttendanceRecordDto>> GetEmployeeAttendanceHistoryAsync(int EmployeeId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                fromDate ??= DateTime.Now.AddDays(-30);
                toDate ??= DateTime.Now;

                // Get all schedule IDs for this employee
                var ScheduleIds = await _context.Schedules
                    .Where(s => s.EmployeeId == EmployeeId)
                    .Select(s => s.ScheduleId)
                    .ToListAsync();

                // Get attendance records via schedule IDs
                var records = await _context.Attendances
                    .Where(a => ScheduleIds.Contains(a.ScheduleId) && a.AttendanceDate >= DateOnly.FromDateTime(fromDate.Value) && a.AttendanceDate <= DateOnly.FromDateTime(toDate.Value))
                    .Include(a => a.Schedule)
                    .AsNoTracking()
                    .OrderByDescending(a => a.AttendanceDate)
                    .ToListAsync();

                return records.Select(a => {
                    // Calculate IsLate dynamically
                    var isLate = false;
                    if (a.ActualCheckIn != null && a.Schedule != null)
                    {
                        var graceTime = a.Schedule.ScheduledStart.AddMinutes(15);
                        isLate = TimeOnly.FromTimeSpan(a.ActualCheckIn.Value.TimeOfDay) > graceTime;
                    }

                    return new AttendanceRecordDto
                    {
                        AttendanceId = a.AttendanceId,
                        EmployeeId = a.Schedule?.EmployeeId ?? 0,
                        AttendanceDate = a.AttendanceDate.ToDateTime(TimeOnly.MinValue),
                        ScheduleStart = a.Schedule?.ScheduledStart.ToTimeSpan(),
                        ScheduleEnd = a.Schedule?.ScheduledEnd.ToTimeSpan(),
                        ActualCheckIn = a.ActualCheckIn,
                        ActualCheckOut = a.ActualCheckOut,
                        RegularHours = a.RegularHours ?? 0,
                        OvertimeHours = a.OvertimeHours ?? 0,
                        AttendanceStatus = a.AttendanceStatus ?? "Present",
                        IsLate = isLate,
                        Notes = a.Notes,
                        HoursWorked = a.ActualCheckIn.HasValue && a.ActualCheckOut.HasValue
                            ? (decimal)(a.ActualCheckOut.Value - a.ActualCheckIn.Value).TotalHours
                            : 0
                    };
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting attendance history: {ex.Message}");
                return new List<AttendanceRecordDto>();
            }
        }

        /// <summary>
        /// Creates a new schedule for an employee on a specific date
        /// </summary>
        public async Task<bool> CreateScheduleAsync(int EmployeeId, DateTime scheduleDate, TimeSpan startTime, TimeSpan endTime, string? notes = null)
        {
            try
            {
                var existingSchedule = await _context.Schedules
                    .FirstOrDefaultAsync(s => s.EmployeeId == EmployeeId && s.ScheduleDate == DateOnly.FromDateTime(scheduleDate) && s.IsActive == true);

                if (existingSchedule != null)
                {
                    // Deactivate old schedule
                    existingSchedule.IsActive = false;
                    existingSchedule.UpdatedAt = DateTime.UtcNow;
                    _context.Schedules.Update(existingSchedule);
                }

                // Always create new schedule
                var newSchedule = new Schedule
                {
                    EmployeeId = EmployeeId,
                    ScheduleDate = DateOnly.FromDateTime(scheduleDate),
                    ScheduledStart = TimeOnly.FromTimeSpan(startTime),
                    ScheduledEnd = TimeOnly.FromTimeSpan(endTime),
                    IsActive = true,
                    Notes = notes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.Schedules.AddAsync(newSchedule);
                await _context.SaveChangesAsync();

                // Update attendance records from old to new schedule
                if (existingSchedule != null)
                {
                    var attendancesToUpdate = await _context.Attendances
                        .Where(a => a.ScheduleId == existingSchedule.ScheduleId)
                        .ToListAsync();

                    foreach (var att in attendancesToUpdate)
                    {
                        att.ScheduleId = newSchedule.ScheduleId;
                        att.UpdatedAt = DateTime.UtcNow;
                    }
                    await _context.SaveChangesAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating schedule: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets attendance summary for a specific date
        /// </summary>
        public async Task<AttendanceSummaryDto> GetAttendanceSummaryAsync(DateTime attendanceDate)
        {
            try
            {
                // Get schedules for this date
                var schedules = await _context.Schedules
                    .Where(s => s.ScheduleDate == DateOnly.FromDateTime(attendanceDate) && s.IsActive == true)
                    .AsNoTracking()
                    .ToListAsync();

                var ScheduleIds = schedules.Select(s => s.ScheduleId).ToList();

                // Get attendance records via schedule
                var records = await _context.Attendances
                    .Where(a => a.AttendanceDate == DateOnly.FromDateTime(attendanceDate) && ScheduleIds.Contains(a.ScheduleId))
                    .Include(a => a.Schedule)
                    .AsNoTracking()
                    .ToListAsync();

                var totalActiveEmployees = await _context.Employees
                    .CountAsync(e => e.Status == "Active");

                var presentCount = records.Count(a => a.AttendanceStatus == "Present");

                // Calculate late count dynamically
                var lateCount = records.Count(a => 
                    a.ActualCheckIn != null && 
                    a.Schedule != null && 
                    a.ActualCheckIn.Value.TimeOfDay > a.Schedule.ScheduledStart.AddMinutes(15).ToTimeSpan());

                return new AttendanceSummaryDto
                {
                    PresentCount = presentCount,
                    AbsentCount = totalActiveEmployees - presentCount - records.Count(a => a.AttendanceStatus == "On Leave"),
                    OnLeaveCount = records.Count(a => a.AttendanceStatus == "On Leave"),
                    LateCount = lateCount,
                    TotalRecords = totalActiveEmployees
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting attendance summary: {ex.Message}");
                return new AttendanceSummaryDto();
            }
        }
    }

    // DTOs for data transfer
    public class AttendanceRecordDto
    {
        public int AttendanceId { get; set; }
        public int EmployeeId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Position { get; set; } = "";
        public DateTime AttendanceDate { get; set; }
        public TimeSpan? ScheduleStart { get; set; }
        public TimeSpan? ScheduleEnd { get; set; }
        public DateTime? ActualCheckIn { get; set; }
        public DateTime? ActualCheckOut { get; set; }
        public decimal RegularHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public string AttendanceStatus { get; set; } = "";
        public bool IsLate { get; set; }
        public string? Notes { get; set; }
        public decimal HoursWorked { get; set; }
    }

    public class EmployeeWithScheduleDto
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Position { get; set; } = "";
        public int? ScheduleId { get; set; }
        public TimeSpan? ScheduleStart { get; set; }
        public TimeSpan? ScheduleEnd { get; set; }
        public int? AttendanceId { get; set; }
        public DateTime? ActualCheckIn { get; set; }
        public DateTime? ActualCheckOut { get; set; }
        public DateTime AttendanceDate { get; set; }
        public string AttendanceStatus { get; set; } = "Not Scheduled";
        public bool IsLate { get; set; }
        public decimal RegularHours { get; set; }
        public decimal OvertimeHours { get; set; }
        public string? Notes { get; set; }
    }

    public class EmployeeDto
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Position { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class AttendanceSummaryDto
    {
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int OnLeaveCount { get; set; }
        public int LateCount { get; set; }
        public int TotalRecords { get; set; }
        public int NotScheduledCount { get; set; }
    }
}
