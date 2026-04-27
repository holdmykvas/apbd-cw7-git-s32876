using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Tutorial7.DTOs;

namespace Tutorial7.Services;

public class AppointmentsService : IAppointmentsService
{
    private readonly string _connectionString;

    public AppointmentsService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string not found");
    }
    
    public async Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(string? status, string? patientLastName)
    {
        var query = @"Select 
            a.IdAppointment,
            a.AppointmentDate,
            a.Status,
            a.Reason,
            p.FirstName +  ' ' + p.LastName AS PatientFullName,
            p.Email AS PatientEmail
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
            WHERE (@Status IS NULL OR a.Status = @Status)
             AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
             ORDER BY a.AppointmentDate;";
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query,connection);
        command.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar) { Value = (object?)status ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@PatientLastName", SqlDbType.NVarChar) { Value = (object?)patientLastName ?? DBNull.Value });
        
        var reader = await command.ExecuteReaderAsync();
        
        var appointments = new List<AppointmentListDto>();
        while (await reader.ReadAsync())
        {
            appointments.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
                AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
                Status =  reader.GetString(reader.GetOrdinal("Status")),
                Reason = reader.GetString(reader.GetOrdinal("Reason")),
                PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
                PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
            });
        }
        
        return appointments;
    }

    public async Task<AppointmentDetailsDTO?> GetAppointmentByIdAsync(int idAppointment)
    {
        var query = @"SELECT 
            a.IdAppointment, a.AppointmentDate,a.Status, a.Reason, a.InternalNotes, a.CreatedAt,
            p.FirstName + ' ' + p.LastName AS PatientFullName,
            p.Email AS PatientEmail,
            p.PhoneNumber AS PatientPhoneNumber,
            d.FirstName + ' ' + d.LastName AS DoctorFullName,
            d.LicenseNumber AS DoctorLicenseNumber
            FROM dbo.Appointments a
            JOIN dbo.Patients p ON a.IdPatient = p.IdPatient
            JOIN dbo.Doctors d ON a.IdDoctor = d.IdDoctor
            WHERE a.IdAppointment = @IdAppointment;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add(new SqlParameter("@IdAppointment", SqlDbType.Int) { Value = idAppointment });
        
        await using var reader = await command.ExecuteReaderAsync();
        
        if (!await reader.ReadAsync())
        {
            return null; 
        }
        
        return new AppointmentDetailsDTO
        {
            IdAppointment = reader.GetInt32(reader.GetOrdinal("IdAppointment")),
            AppointmentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate")),
            Status = reader.GetString(reader.GetOrdinal("Status")),
            Reason = reader.GetString(reader.GetOrdinal("Reason")),
            InternalNotes = reader.IsDBNull(reader.GetOrdinal("InternalNotes")) ? null : reader.GetString(reader.GetOrdinal("InternalNotes")),
            CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
        
            PatientFullName = reader.GetString(reader.GetOrdinal("PatientFullName")),
            PatientEmail = reader.GetString(reader.GetOrdinal("PatientEmail")),
            PatientPhoneNumber = reader.GetString(reader.GetOrdinal("PatientPhoneNumber")),
        
            DoctorFullName = reader.GetString(reader.GetOrdinal("DoctorFullName")),
            DoctorLicenseNumber = reader.GetString(reader.GetOrdinal("DoctorLicenseNumber"))
        };
    }

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDTO request)
    {
        if (request.AppointmentDate < DateTime.Now)
        {
            throw new ArgumentException("Appointment data can't be in past");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var PatientQuery = "SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @IdPatient AND IsActive = 1";
        await using var patientCmd = new SqlCommand(PatientQuery, connection);
        patientCmd.Parameters.Add(new SqlParameter("@IdPatient", SqlDbType.Int) { Value = request.IdPatient });
        var patientExists = (int)await patientCmd.ExecuteScalarAsync() > 0;

        if (!patientExists)
        {
            throw new ArgumentException("Patient doesn't exist or is not active");
        }
        
        var DoctorQuery = "SELECT COUNT(1) FROM dbo.Doctors WHERE IdDoctor = @IdDoctor AND IsActive = 1";
        await using var DoctorCmd = new SqlCommand(DoctorQuery, connection);
        DoctorCmd.Parameters.Add(new SqlParameter("@IdDoctor", SqlDbType.Int) { Value = request.IdDoctor });
        var doctorExists = (int)await DoctorCmd.ExecuteScalarAsync() > 0;

        if (!doctorExists)
        {
            throw new ArgumentException("Doctor doesn't exist or is not active");
        }
        
        var conflictQuery = "SELECT COUNT(1) FROM dbo.Appointments WHERE IdDoctor = @IdDoctor AND AppointmentDate = @AppointmentDate";
        await using var ConflictCmd = new SqlCommand(conflictQuery, connection);
        ConflictCmd.Parameters.Add(new SqlParameter("@IdDoctor", SqlDbType.Int) { Value = request.IdDoctor });
        ConflictCmd.Parameters.Add(new SqlParameter("@AppointmentDate", SqlDbType.DateTime2) { Value = request.AppointmentDate });
        var hasConflict = (int)await ConflictCmd.ExecuteScalarAsync() > 0;
    
        if (hasConflict)
        {
            throw new InvalidOperationException("Doctor already has an appointment at this specific time");
        }
        
        var insertQuery = @"
        INSERT INTO dbo.Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
        OUTPUT INSERTED.IdAppointment
        VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason);";

        await using var insertCmd = new SqlCommand(insertQuery, connection);
        insertCmd.Parameters.Add(new SqlParameter("@IdPatient", SqlDbType.Int) { Value = request.IdPatient });
        insertCmd.Parameters.Add(new SqlParameter("@IdDoctor", SqlDbType.Int) { Value = request.IdDoctor });
        insertCmd.Parameters.Add(new SqlParameter("@AppointmentDate", SqlDbType.DateTime2) { Value = request.AppointmentDate });
        insertCmd.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar) { Value = request.Reason });
        
        var newId = (int)await insertCmd.ExecuteScalarAsync();
    
        return newId;
    }

    public async Task UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDTO request)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var checkQuery = "SELECT AppointmentDate, Status FROM dbo.Appointments WHERE IdAppointment = @IdAppointment";
        await using var checkCmd = new SqlCommand(checkQuery, connection);
        checkCmd.Parameters.Add(new SqlParameter("@IdAppointment", SqlDbType.Int) { Value = idAppointment });

        DateTime currentDate;
        string currentStatus;

        await using (var reader = await checkCmd.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                throw new KeyNotFoundException($"Appointment {idAppointment} not found");
            }
            
            currentDate = reader.GetDateTime(reader.GetOrdinal("AppointmentDate"));
            currentStatus = reader.GetString(reader.GetOrdinal("Status"));
        }
        
        if (request.Status != "Scheduled" && request.Status != "Completed" && request.Status != "Canceled")
        {
            throw new ArgumentException("Status must be exactly: Scheduled, Completed, or Canceled");
        }
        
        if (currentStatus == "Completed" && currentDate != request.AppointmentDate)
        {
            throw new ArgumentException($"Appointment {idAppointment} is already completed. Data change is impossible");
        }

        var patientQuery = "SELECT COUNT(1) FROM dbo.Patients WHERE IdPatient = @IdPatient AND  IsActive = 1";
        await using var patientCmd = new SqlCommand(patientQuery, connection);
        patientCmd.Parameters.Add(new SqlParameter("@IdPatient", SqlDbType.Int) { Value = request.IdPatient });
        if ((int)await patientCmd.ExecuteScalarAsync() == 0)
        {
            throw new InvalidOperationException("Patient doesn't exist or is not active");
        }
        
        var doctorQuery = "SELECT COUNT(1) FROM dbo.Patients WHERE IdDoctor = @IdDoctor AND  IsActive = 1";
        await using var doctorCmd = new SqlCommand(doctorQuery, connection);
        doctorCmd.Parameters.Add(new SqlParameter("@IdDoctor", SqlDbType.Int) { Value = request.IdDoctor });
        if ((int)await patientCmd.ExecuteScalarAsync() == 0)
        {
            throw new InvalidOperationException("Doctor doesn't exist or is not active");
        }
        
        if (currentDate != request.AppointmentDate)
        {
            var conflictQuery = "SELECT COUNT(1) FROM dbo.Appointments WHERE IdDoctor = @IdDoctor AND AppointmentDate = @AppointmentDate AND IdAppointment != @IdAppointment";
            await using var conflictCmd = new SqlCommand(conflictQuery, connection);
            conflictCmd.Parameters.Add(new SqlParameter("@IdDoctor", SqlDbType.Int) { Value = request.IdDoctor });
            conflictCmd.Parameters.Add(new SqlParameter("@AppointmentDate", SqlDbType.DateTime2) { Value = request.AppointmentDate });
            conflictCmd.Parameters.Add(new SqlParameter("@IdAppointment", SqlDbType.Int) { Value = idAppointment });
            
            if ((int)await conflictCmd.ExecuteScalarAsync() > 0)
            {
                throw new InvalidOperationException("Doctor already has an appointment at this specific time.");
            }
        }
        
        var updateQuery = @"
            UPDATE dbo.Appointments 
            SET IdPatient = @IdPatient, 
                IdDoctor = @IdDoctor, 
                AppointmentDate = @AppointmentDate, 
                Status = @Status, 
                Reason = @Reason, 
                InternalNotes = @InternalNotes 
            WHERE IdAppointment = @IdAppointment";
            
        await using var updateCmd = new SqlCommand(updateQuery, connection);
        updateCmd.Parameters.Add(new SqlParameter("@IdPatient", SqlDbType.Int) { Value = request.IdPatient });
        updateCmd.Parameters.Add(new SqlParameter("@IdDoctor", SqlDbType.Int) { Value = request.IdDoctor });
        updateCmd.Parameters.Add(new SqlParameter("@AppointmentDate", SqlDbType.DateTime2) { Value = request.AppointmentDate });
        updateCmd.Parameters.Add(new SqlParameter("@Status", SqlDbType.NVarChar) { Value = request.Status });
        updateCmd.Parameters.Add(new SqlParameter("@Reason", SqlDbType.NVarChar) { Value = request.Reason });
        
        // Handle potential Nulls safely for the database
        updateCmd.Parameters.Add(new SqlParameter("@InternalNotes", SqlDbType.NVarChar) { Value = (object?)request.InternalNotes ?? DBNull.Value });
        updateCmd.Parameters.Add(new SqlParameter("@IdAppointment", SqlDbType.Int) { Value = idAppointment });

        await updateCmd.ExecuteNonQueryAsync();
    }
}