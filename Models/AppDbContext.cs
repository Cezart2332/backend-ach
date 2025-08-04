using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.Auth;

namespace WebApplication1.Models
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Company> Companies { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<LocationHour> LocationHours { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<BugReport> BugReports { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Companies
            modelBuilder.Entity<Company>().ToTable("companies");
            
            // Configure Locations
            modelBuilder.Entity<Location>().ToTable("locations");
            modelBuilder.Entity<Location>()
                .HasOne(l => l.Company)
                .WithMany(c => c.Locations)
                .HasForeignKey(l => l.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Location>()
                .HasIndex(l => new { l.CompanyId, l.Name })
                .IsUnique();
            
            // Configure LocationHours
            modelBuilder.Entity<LocationHour>().ToTable("location_hours");
            modelBuilder.Entity<LocationHour>()
                .Property(lh => lh.DayOfWeek)
                .HasConversion<string>()
                .IsRequired();
                
            modelBuilder.Entity<LocationHour>()
                .HasIndex(lh => new { lh.LocationId, lh.DayOfWeek })
                .IsUnique();
                
            modelBuilder.Entity<LocationHour>()
                .HasOne(lh => lh.Location)
                .WithMany(l => l.LocationHours)
                .HasForeignKey(lh => lh.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Configure Events
            modelBuilder.Entity<Event>().ToTable("events");
            modelBuilder.Entity<Event>()
                .HasOne(e => e.Company)
                .WithMany(c => c.Events)
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Explicitly ignore any LocationId property that EF might try to infer
            modelBuilder.Entity<Event>().Ignore("LocationId");
            
    
            // Configure Address and City column types
            modelBuilder.Entity<Event>()
                .Property(e => e.Address)
                .HasColumnType("varchar(500)");
                
            modelBuilder.Entity<Event>()
                .Property(e => e.City)
                .HasColumnType("varchar(200)");
            
            // Configure Users
            modelBuilder.Entity<User>().ToTable("users");
            
            // Configure Reservations
            modelBuilder.Entity<Reservation>().ToTable("reservations");
            modelBuilder.Entity<Reservation>()
                .Property(r => r.Status)
                .HasConversion<string>()
                .IsRequired();
                
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Location)
                .WithMany(l => l.Reservations)
                .HasForeignKey(r => r.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.SetNull);
                
            modelBuilder.Entity<Reservation>()
                .HasIndex(r => new { r.LocationId, r.ReservationDate, r.ReservationTime })
                .IsUnique(false);
                
            modelBuilder.Entity<Reservation>()
                .HasIndex(r => r.Status);
                
            modelBuilder.Entity<Reservation>()
                .HasIndex(r => r.CreatedAt);
                
            // Configure Likes
            modelBuilder.Entity<Like>().ToTable("likes");
            modelBuilder.Entity<Like>()
                .HasOne(l => l.Event)
                .WithMany()
                .HasForeignKey(l => l.EventId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Like>()
                .HasOne(l => l.User)
                .WithMany()
                .HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Ensure unique constraint - one like per user per event
            modelBuilder.Entity<Like>()
                .HasIndex(l => new { l.EventId, l.UserId })
                .IsUnique();
                
            modelBuilder.Entity<Like>()
                .HasIndex(l => l.CreatedAt);
                
            // Configure MenuItems
            modelBuilder.Entity<MenuItem>().ToTable("menu_items");
            modelBuilder.Entity<MenuItem>()
                .HasOne(mi => mi.Location)
                .WithMany(l => l.MenuItems)
                .HasForeignKey(mi => mi.LocationId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<MenuItem>()
                .HasIndex(mi => mi.LocationId);
                
            modelBuilder.Entity<MenuItem>()
                .HasIndex(mi => mi.Category);
                
            // Configure RefreshTokens
            modelBuilder.Entity<RefreshToken>().ToTable("refresh_tokens");
            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.Token)
                .IsUnique();
                
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.UserId);
                
            modelBuilder.Entity<RefreshToken>()
                .HasIndex(rt => rt.ExpiresAt);
                
            // Configure Users security indices
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();
                
            modelBuilder.Entity<User>()
                .HasIndex(u => u.LastLoginAt);
                
            // Configure BugReports
            modelBuilder.Entity<BugReport>().ToTable("bug_reports");
            modelBuilder.Entity<BugReport>()
                .HasIndex(br => br.CreatedAt);
                
            modelBuilder.Entity<BugReport>()
                .HasIndex(br => br.IsResolved);
                
            modelBuilder.Entity<BugReport>()
                .HasIndex(br => br.Username);
        }
    }
}