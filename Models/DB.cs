using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
#nullable disable warnings

namespace SHAPEE.Models;

public class DB(DbContextOptions options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Staff> Staffs { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Program> Programs { get; set; }
    public DbSet<Faculty> Faculties { get; set; }

}

public class User
{

    [Key, MaxLength(50)]
    public string Id { get; set; }

    [MaxLength(100)]
    public string? FirstName { get; set; }

    [MaxLength(100)]
    public string? LastName { get; set; }

    [MaxLength(100)]
    public string Email { get; set; }

    [MaxLength(100)]
    public string Hash { get; set; }

    [MaxLength(100)]
    public string? Contact { get; set; }

    public string Role => GetType().Name;

    public int LoginAttempts { get; set; } = 0;

    [MaxLength(20)]
    public string Status { get; set; } = "Active"; //Active, Locked

    [NotMapped]
    public string FullName => $"{LastName} {FirstName} ";
}

public class Admin : User
{
}

public class Staff : User
{
    public DateOnly DateJoin { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    [MaxLength(10)]
    public string Gender { get; set; }

    [MaxLength(200)]
    public string ProfilePictureUrl { get; set; }

    //FK to faculty -- 1 staff belong to 1 faculty
    public string FacultyId { get; set; }
    public Faculty Faculty { get; set; }
}

public class Student : User
{
    public DateOnly DateJoin { get; set; } = DateOnly.FromDateTime(DateTime.Now);
    public int YearLevel { get; set; }
    public int sem { get; set; }

    [MaxLength(10)]
    public string Gender { get; set; }

    [MaxLength(200)]
    public string ProfilePictureUrl { get; set; }

    //FK to Program -- 1 user belong to 1 program
    public string ProgramId { get; set; }

    public Program Program { get; set; }


}

public class Program
{
    [Key, MaxLength(50)]
    public string Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }

    //FK to faculty -- 1 program belongs to one faculty
    public string FacultyId { get; set; }
    public Faculty Faculty { get; set; }

    //1 program have many students
    public List<Student> Students { get; set; } = [];

    //Many to Many Relationship for Program and Course
    public List<ProgramCourse> ProgramCourses { get; set; }

}

public class Faculty
{
    [Key, MaxLength(50)]
    public string Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }

    // 1 Faculty have many programs
    public List<Program> Programs { get; set; } = [];

    //1 faculty have many staffs
    public List<Staff> Staffs { get; set; } = [];
}

public class Course
{
    [Key, MaxLength(10)]
    public string CourseCode { get; set; }

    [MaxLength(100)]
    public string Name { get; set; }

    public int CreditHours { get; set; }

    //Many to Many Relationship for Program and Course
    public List<ProgramCourse> ProgramCourses { get; set; }

}

public class ProgramCourse //Define the program have what course in each year and semester
{ 
    //FK to Program
    public string ProgramId { get; set; }
    public Program Program { get; set; }

    //FK to Course
    public string CourseCode { get; set; }
    public Course Course {  get; set; }

    public int YearLevel { get; set; }
    public int Semester {  get; set; }
}

public class ClassSection
{

}



