using System;
using System.ComponentModel.DataAnnotations;
using PIOGHOASIS.Models;

namespace PIOGHOASIS.Models.ViewModels
{
    public class EmpleadoCreateVM
    {
        public Persona Persona { get; set; } = new Persona();
        public Empleado Empleado { get; set; } = new Empleado();

    }

}
