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
        //public DbSet<Empleado> empleados { get; set; } = null!;
        public DbSet<Persona> personas => Set<Persona>();
        public DbSet<Rol> roles => Set<Rol>();
        public DbSet<Puesto> puestos => Set<Puesto>();

        public DbSet<PIOGHOASIS.Models.TipoDocumento> tipoDocumentos { get; set; } = null!;
        public DbSet<Pais> paises { get; set; } = null!;
        public DbSet<Departamento> departamentos { get; set; } = null!;
        public DbSet<Municipio> municipios { get; set; } = null!;
        //public DbSet<PasswordResetToken> password_reset_tokens { get; set; } = null!;
        public DbSet<PasswordResetToken> password_reset_tokens => Set<PasswordResetToken>();

        //public DbSet<Cliente> clientes { get; set; } = null!;
        public DbSet<Cliente> clientes => Set<Cliente>();


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Aplica todas las configuraciones en el ensamblado
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            //base.OnModelCreating(modelBuilder);
            // Default de Estado en PUESTO (opcional)
            modelBuilder.Entity<Puesto>()
                .Property(p => p.Estado)
                .HasDefaultValue(true);

            // EMPLEADO -> PERSONA (1–1), FK: PersonalID -> PersonaID
            modelBuilder.Entity<Empleado>()
                .HasOne(e => e.Persona)
                .WithOne(p => p.Empleado)
                .HasForeignKey<Empleado>(e => e.PersonalID)
                .HasPrincipalKey<Persona>(p => p.PersonaID);

            // EMPLEADO -> PUESTO (N–1), FK: PuestoID -> PuestoID
            //modelBuilder.Entity<Empleado>()
            //    .HasOne(e => e.Puesto)
            //    .WithMany()  // si no tienes colección en Puesto
            //    .HasForeignKey(e => e.PuestoID)
            //    .HasPrincipalKey<Puesto>(p => p.PuestoID);

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

            // Relación PAIS (1) — (N) DEPARTAMENTO
            modelBuilder.Entity<Departamento>()
                .HasOne(d => d.Pais)
                .WithMany(p => p.Departamentos)
                .HasForeignKey(d => d.PaisID)
                .HasPrincipalKey(p => p.PaisID);

            //modelBuilder.Entity<PasswordResetToken>(b =>
            //{
            //    b.ToTable("PASSWORD_RESET_TOKENS", "dbo");
            //    b.HasKey(x => x.Id);
            //    b.Property(x => x.UsuarioID).HasMaxLength(20).IsRequired();
            //    b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();

            //    b.HasIndex(x => new { x.UsuarioID, x.TokenHash }).IsUnique();
            //    b.HasIndex(x => x.ExpiresAtUtc);

            //    b.HasOne<Usuario>()                           // sin navegación
            //     .WithMany()
            //     .HasForeignKey(x => x.UsuarioID)
            //     .HasPrincipalKey(u => u.UsuarioID)          // PK de USUARIO es string
            //     .OnDelete(DeleteBehavior.Cascade);
            //});

            // PASSWORD_RESET_TOKENS
            modelBuilder.Entity<PasswordResetToken>(eb =>
            {
                eb.ToTable("PASSWORD_RESET_TOKENS", "dbo");

                eb.HasKey(t => t.Id);

                eb.Property(t => t.UsuarioID)
                  .HasColumnName("UsuarioID")
                  .HasMaxLength(10)
                  .IsRequired();

                eb.Property(t => t.TokenHash)
                  .HasMaxLength(64)
                  .IsRequired();

                // Relación clara: t.UsuarioID ---> u.UsuarioID
                eb.HasOne(t => t.Usuario)
                  .WithMany()                              // (o .WithMany(u => u.PasswordResetTokens) si agregas la colección)
                  .HasForeignKey(t => t.UsuarioID)         // FK en PasswordResetToken
                  .HasPrincipalKey(u => u.UsuarioID)       // PK en Usuario
                  .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder);

        }
    }
}
