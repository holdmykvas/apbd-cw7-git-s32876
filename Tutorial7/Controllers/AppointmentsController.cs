using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Tutorial7.DTOs;
using Tutorial7.Services;

namespace Tutorial7.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly IAppointmentsService _appointmentsService;

        public AppointmentsController(IAppointmentsService appointmentsService)
        {
            _appointmentsService = appointmentsService;
        }
        
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string? status, [FromQuery] string? patientLastName)
        {
            var appointments = await _appointmentsService.GetAllAppointmentsAsync(status, patientLastName);
            return Ok(appointments);
        }

        [HttpGet("{idAppointment:int}")]
        public async Task<IActionResult> GetById([FromRoute] int idAppointment)
        {
            var appointment = await _appointmentsService.GetAppointmentByIdAsync(idAppointment);

            if (appointment == null)
            {
                return NotFound($"Appointment {idAppointment} not found");
            }
            return Ok(appointment);
        }
    }
}
