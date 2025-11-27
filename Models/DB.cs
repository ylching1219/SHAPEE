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

        [MaxLength(50)]
        public string? ResetToken { get; set; } // Stores the unique token sent via email
        public DateTime? TokenExpiry { get; set; } // Stores the token expiration time (UTC)
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