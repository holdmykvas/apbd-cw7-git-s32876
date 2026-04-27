using System.Collections.Generic;
using System.Threading.Tasks;
using Tutorial7.DTOs;

namespace Tutorial7.Services;

public interface IAppointmentsService
{
    Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(string? status,string? patientLastName);
    
    Task<AppointmentDetailsDTO?> GetAppointmentByIdAsync(int idAppointment);
    
    Task<int> CreateAppointmentAsync(CreateAppointmentRequestDTO request);
}