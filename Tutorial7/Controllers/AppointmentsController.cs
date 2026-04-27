using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentRequestDTO request)
        {
            try
            {
                var newAppointmentId = await _appointmentsService.CreateAppointmentAsync(request);

                return CreatedAtAction(nameof(GetById), new { idAppointment = newAppointmentId }, null);
            }
            catch (ArgumentException e)
            {
                return BadRequest(new ErrorResponseDTO { Message = e.Message});
            }
            catch (InvalidOperationException e)
            {
                return Conflict(new ErrorResponseDTO { Message = e.Message});
            }
        }

        [HttpPut("{idAppointment:int}")]
        public async Task<IActionResult> Update([FromRoute] int idAppointment,
            [FromBody] UpdateAppointmentRequestDTO request)
        {
            try
            {
                await _appointmentsService.UpdateAppointmentAsync(idAppointment, request);
                return Ok($"Appointment {idAppointment} updated");
            }
            catch (KeyNotFoundException e)
            {
                return NotFound(new ErrorResponseDTO { Message = e.Message});
            }
            catch (ArgumentException e)
            {
                return BadRequest(new ErrorResponseDTO { Message = e.Message});
            } catch (InvalidOperationException e)
            {
                return Conflict(new ErrorResponseDTO { Message = e.Message});
            }
        }

        [HttpDelete("{idAppointment:int}")]
        public async Task<IActionResult> Delete([FromRoute] int idAppointment)
        {
            try
            {
                await _appointmentsService.DeleteAppointmentAsync(idAppointment);
                return NoContent();
            }
            catch (KeyNotFoundException e)
            {
                return NotFound(new ErrorResponseDTO { Message = e.Message});
            }
            catch (InvalidOperationException e)
            {
                return Conflict(new ErrorResponseDTO { Message = e.Message});
            }
        }
    }
}
