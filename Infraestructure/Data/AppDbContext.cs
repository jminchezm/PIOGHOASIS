using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Models;
using PIOGHOASIS.Models.Entities;

namespace PIOGHOASIS.Infraestructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt) { }

        public DbSet<Usuario> usuarios => Set<Usuario>();
        public DbSet<Empleado> empleados => Set<Empleado>();
        public DbSet<Persona> personas => Set<Persona>();
        public DbSet<Rol> roles => Set<Rol>();
        public DbSet<Puesto> puestos => Set<Puesto>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Aplica todas las configuraciones en el ensamblado
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            base.OnModelCreating(modelBuilder);

            // PERSONA 1–1 EMPLEADO (FK: Empleado.PersonalID)
            modelBuilder.Entity<Empleado>()
                .HasOne(e => e.Persona)
                .WithOne(p => p.Empleado)
                .HasForeignKey<Empleado>(e => e.PersonalID)
                .OnDelete(DeleteBehavior.Restrict);

            // EMPLEADO 1–1 USUARIO (FK: Usuario.EmpleadoID)
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Empleado)
                .WithOne(e => e.Usuario)
                .HasForeignKey<Usuario>(u => u.EmpleadoID)
                .OnDelete(DeleteBehavior.Restrict);

            // USUARIO N..1 ROL (FK: Usuario.RolID)
            modelBuilder.Entity<Usuario>()
                .HasOne(u => u.Rol)
                .WithMany(r => r.Usuarios)
                .HasForeignKey(u => u.RolID)
                .OnDelete(DeleteBehavior.Restrict);

            // Default de Estado en PUESTO (opcional)
            modelBuilder.Entity<Puesto>()
                .Property(p => p.Estado)
                .HasDefaultValue(true);


            base.OnModelCreating(modelBuilder);

        }
    }
}
