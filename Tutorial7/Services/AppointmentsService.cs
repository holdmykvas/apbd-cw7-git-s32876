using System.Data;
using Microsoft.Data.SqlClient;
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
}