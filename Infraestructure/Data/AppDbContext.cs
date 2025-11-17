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

        public DbSet<PIOGHOASIS.Models.TipoDocumento> tipoDocumentos { get; set; } = null!;
        public DbSet<Pais> paises { get; set; } = null!;
        public DbSet<Departamento> departamentos { get; set; } = null!;
        public DbSet<Municipio> municipios { get; set; } = null!;
        public DbSet<PasswordResetToken> password_reset_tokens => Set<PasswordResetToken>();

        public DbSet<Cliente> clientes => Set<Cliente>();

        public DbSet<TipoHabitacion> tiposHabitacion { get; set; } = default!;
        public DbSet<Habitacion> habitaciones => Set<Habitacion>();
        public DbSet<TarifaHabitacion> tarifasHabitacion { get; set; }

        public DbSet<Reserva> reservas => Set<Reserva>();
        public DbSet<DetalleReserva> detalleReservas => Set<DetalleReserva>();
        public DbSet<EstadoReserva> estadosReserva => Set<EstadoReserva>();
        public DbSet<PagoReserva> pagosReserva => Set<PagoReserva>();
        public DbSet<FormaPago> formasPago => Set<FormaPago>();
        public DbSet<TipoPago> tiposPago => Set<TipoPago>();
        public DbSet<PlataformaReserva> plataformasReserva { get; set; } = null!;

        public DbSet<Modulo> modulos { get; set; } = default!;

        public DbSet<RolModuloPermiso> rolModuloPermisos { get; set; } = default!;

        public DbSet<Caja> cajas { get; set; } = default!;
        public DbSet<CajaPago> cajaPagos { get; set; } = default!;
        public DbSet<EstadoCaja> estadosCaja { get; set; } = default!;
        public DbSet<CajaAjuste> cajaAjustes { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            base.OnModelCreating(modelBuilder);

            // Aplica todas las configuraciones en el ensamblado
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
            modelBuilder.Entity<Puesto>()
                .Property(p => p.Estado)
                .HasDefaultValue(true);

            // EMPLEADO -> PERSONA (1–1), FK: PersonalID -> PersonaID
            modelBuilder.Entity<Empleado>()
                .HasOne(e => e.Persona)
                .WithOne(p => p.Empleado)
                .HasForeignKey<Empleado>(e => e.PersonalID)
                .HasPrincipalKey<Persona>(p => p.PersonaID);

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
                  .WithMany()                              
                  .HasForeignKey(t => t.UsuarioID)         // FK en PasswordResetToken
                  .HasPrincipalKey(u => u.UsuarioID)       // PK en Usuario
                  .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TipoHabitacion>(e =>
            {
                e.ToTable("TIPO_HABITACION");
                e.HasKey(x => x.TipoHabitacionID);
                e.Property(x => x.TipoHabitacionID).HasMaxLength(10).IsUnicode(true);
                e.Property(x => x.Nombre).HasMaxLength(100).IsUnicode(true).IsRequired();
                e.Property(x => x.Descripcion).HasMaxLength(300).IsUnicode(true);
                // Estado -> bit, mapeo por convención
            });

            modelBuilder.Entity<Habitacion>(e =>
            {
                e.ToTable("HABITACION");
                e.HasKey(x => x.HabitacionID);
                e.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
                e.Property(x => x.TipoHabitacionID).HasMaxLength(10).IsRequired();
                e.HasOne(x => x.TipoHabitacion)
                 .WithMany()
                 .HasForeignKey(x => x.TipoHabitacionID)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Si todo está en dbo, fija el esquema por defecto y te ahorras repetirlo:
            modelBuilder.HasDefaultSchema("dbo");

            // ===== Catálogos / lookups =====
            modelBuilder.Entity<EstadoReserva>(e =>
            {
                e.ToTable("ESTADO_RESERVA", "dbo");
                e.HasKey(x => x.EstadoReservaID);
                //e.Property(x => x.EstadoReservaID).HasColumnType("smallint");
            });

            modelBuilder.Entity<Reserva>(e =>
            {
                e.HasOne(r => r.Estado)
                 .WithMany()
                 .HasForeignKey(r => r.EstadoReservaID);
            });

            modelBuilder.Entity<FormaPago>(e =>
            {
                e.ToTable("FORMA_PAGO");
                e.HasKey(x => x.FormaPagoID);
                e.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
            });

            modelBuilder.Entity<TipoPago>(e =>
            {
                e.ToTable("TIPO_PAGO");
                e.HasKey(x => x.TipoPagoID);
                e.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
            });

            modelBuilder.Entity<PlataformaReserva>(e =>
            {
                e.ToTable("PLATAFORMA_RESERVA");
                e.HasKey(x => x.PlataformaID);           
                e.Property(x => x.Nombre).HasMaxLength(60).IsRequired();
                e.Property(x => x.Codigo).HasMaxLength(20);
            });

            // ===== Personas / clientes / usuarios =====
            modelBuilder.Entity<Persona>(e =>
            {
                e.ToTable("PERSONA");
                e.HasKey(x => x.PersonaID);
                e.Property(x => x.PersonaID).HasMaxLength(10).IsRequired();
                e.Property(x => x.PrimerNombre).HasMaxLength(60).IsRequired();
                e.Property(x => x.PrimerApellido).HasMaxLength(60).IsRequired();
                e.HasIndex(x => x.NumeroDocumento)
                 .IsUnique()
                 .HasDatabaseName("UX_PERSONA_NumeroDocumento");
            });

            modelBuilder.Entity<Cliente>(e =>
            {
                e.ToTable("CLIENTE");
                e.HasKey(x => x.ClienteID);
                e.Property(x => x.ClienteID).HasMaxLength(10).IsRequired();
                e.Property(x => x.PersonaID).HasMaxLength(10).IsRequired();

                // 🔹 Relación formal 1–1: cada cliente tiene una persona única
                e.HasOne(x => x.Persona)
                 .WithOne(p => p.Cliente)
                 .HasForeignKey<Cliente>(x => x.PersonaID)
                 .HasPrincipalKey<Persona>(p => p.PersonaID)
                 .OnDelete(DeleteBehavior.Restrict);
            });


            modelBuilder.Entity<Usuario>(e =>
            {
                e.ToTable("USUARIO");
                e.HasKey(x => x.UsuarioID);
                e.Property(x => x.UsuarioID).HasMaxLength(10);
            });

            // ===== Habitaciones / tarifas =====
            modelBuilder.Entity<TipoHabitacion>(e =>
            {
                e.ToTable("TIPO_HABITACION");
                e.HasKey(x => x.TipoHabitacionID);
                e.Property(x => x.TipoHabitacionID).HasMaxLength(10).IsUnicode(true);
                e.Property(x => x.Nombre).HasMaxLength(100).IsUnicode(true).IsRequired();
                e.Property(x => x.Descripcion).HasMaxLength(300).IsUnicode(true);
            });

            modelBuilder.Entity<Habitacion>(e =>
            {
                e.ToTable("HABITACION");
                e.HasKey(x => x.HabitacionID);
                e.Property(x => x.Codigo).HasMaxLength(10).IsRequired();
                e.Property(x => x.TipoHabitacionID).HasMaxLength(10).IsRequired();

                e.HasOne(x => x.TipoHabitacion)
                 .WithMany()
                 .HasForeignKey(x => x.TipoHabitacionID)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(h => h.Estado);
            });

            modelBuilder.Entity<TarifaHabitacion>(e =>
            {
                e.ToTable("TARIFA_HABITACION");
                e.HasKey(x => x.TarifaID);
                e.Property(x => x.NumeroPersonas).IsRequired();
                e.Property(x => x.PrecioNoche).HasColumnType("decimal(18,2)");

                e.HasOne(x => x.Habitacion)
                 .WithMany()
                 .HasForeignKey(x => x.HabitacionID);

                e.HasIndex(t => new { t.HabitacionID, t.NumeroPersonas, t.FechaInicio, t.FechaFin });
            });

            // ===== Reservas =====
            modelBuilder.Entity<Reserva>(e =>
            {
                e.ToTable("RESERVA");
                e.HasKey(x => x.ReservaID);

                e.Property(x => x.Codigo).HasMaxLength(12).IsRequired();
                e.Property(x => x.ClienteID).HasMaxLength(10).IsRequired();   
                e.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
                e.Property(x => x.Impuestos).HasColumnType("decimal(18,2)");
                e.Property(x => x.Total).HasColumnType("decimal(18,2)");

                e.HasOne(x => x.Cliente)
                 .WithMany()
                 .HasForeignKey(x => x.ClienteID);

                e.HasOne(x => x.Estado)
                 .WithMany()
                 .HasForeignKey(x => x.EstadoReservaID);

                e.HasIndex(r => new { r.FechaCheckIn, r.FechaCheckOut });
            });

            modelBuilder.Entity<DetalleReserva>(e =>
            {
                e.ToTable("DETALLE_RESERVA");
                e.HasKey(x => x.DetalleReservaID);

                e.Property(x => x.PrecioPorNoche).HasColumnType("decimal(18,2)");
                e.Property(x => x.TotalLinea).HasColumnType("decimal(18,2)");

                e.HasOne(d => d.Reserva)
                 .WithMany(r => r.Detalles)
                 .HasForeignKey(d => d.ReservaID);

                e.HasOne(d => d.Habitacion)
                 .WithMany()
                 .HasForeignKey(d => d.HabitacionID);

                e.HasOne(d => d.Tarifa)
                 .WithMany()
                 .HasForeignKey(d => d.TarifaID);
            });

            modelBuilder.Entity<PagoReserva>(e =>
            {
                e.ToTable("PAGO_RESERVA");
                e.HasKey(x => x.PagoReservaID);
                e.Property(x => x.MontoPagado).HasColumnType("decimal(18,2)");

                e.HasOne(p => p.Reserva).WithMany(r => r.Pagos).HasForeignKey(p => p.ReservaID);
                e.HasOne(p => p.FormaPago).WithMany().HasForeignKey(p => p.FormaPagoID);
                e.HasOne(p => p.TipoPago).WithMany().HasForeignKey(p => p.TipoPagoID);
                e.HasOne(p => p.Plataforma).WithMany().HasForeignKey(p => p.PlataformaID);
            });

            // En OnModelCreating
            modelBuilder.Entity<Caja>()
                .HasIndex(c => c.Codigo)
                .IsUnique();

            modelBuilder.Entity<Caja>()
                .Property(c => c.Codigo)
                .ValueGeneratedOnAdd();


        }
    }
}
