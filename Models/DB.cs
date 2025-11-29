using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
#nullable disable warnings

namespace SHAPEE.Models
{
    public class DB(DbContextOptions options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Admin> Admins { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Student> Students { get; set; }
    }

    public class User
    {
        [Key, MaxLength(50)]
        public string Id { get; set; }

        [MaxLength(100)]
        public string Hash { get; set; }

        public string Role => GetType().Name;

        public int LoginAttempts { get; set; }

        [MaxLength(20)]
        public string Status { get; set; }

        [MaxLength(8)]
        public string Discriminator { get; set; }

        [MaxLength(50)]
        public string? ResetToken { get; set; }

        public DateTime? TokenExpiry { get; set; }

        // New fields for user management
        [Required, MaxLength(50)]
        public string FirstName { get; set; }

        [Required, MaxLength(50)]
        public string LastName { get; set; }

        [Required, MaxLength(100)]
        public string Email { get; set; }

        [MaxLength(20)]
        public string? ContactNumber { get; set; }

        [MaxLength(100)]
        public string? Program { get; set; }

        public int? YearLevel { get; set; }

        public DateTime DateJoined { get; set; } = DateTime.Now;

        // Computed property for full name
        public string FullName => $"{FirstName} {LastName}";
    }

    public class Admin : User
    {
    }

    public class Staff : User
    {
    }

    public class Student : User
    {
    }
}