using Microsoft.AspNetCore.Mvc;
using SHAPEE.Models;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class ClassroomController : Controller
{
    private readonly DB _db;

    public ClassroomController(DB db)
    {
        _db = db;
    }

    // =======================
    // TEACHER CLASSWORK PAGE
    // =======================
    public IActionResult Classwork(string id)
    {
        if (string.IsNullOrEmpty(id))
            return BadRequest("Course ID is required.");

        var room = _db.Courses.FirstOrDefault(c => c.Id == id);
        if (room == null)
            return NotFound();

        var materials = _db.Materials
            .Where(m => m.CourseId == id)
            .OrderBy(m => m.Title)
            .ThenBy(m => m.Id)
            .ToList();

        ViewBag.CourseId = id.ToUpper();  
        ViewBag.Room = room;

        return View("~/Views/Staff/Classwork.cshtml", materials);
    }

    // =======================
    // UPLOAD MULTIPLE FILES
    // =======================
    [HttpPost]
    public IActionResult UploadDoc(string courseId, string title, IFormFile[] files)
    {
        if (files == null || files.Length == 0)
            return BadRequest("No files selected.");

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Title is required.");

        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadFolder))
            Directory.CreateDirectory(uploadFolder);

        var lastMaterial = _db.Materials
            .OrderByDescending(m => m.Id)
            .FirstOrDefault();

        int nextNumber = 1;
        if (lastMaterial != null && lastMaterial.Id.StartsWith("M"))
        {
            if (int.TryParse(lastMaterial.Id.Substring(1), out int num))
                nextNumber = num + 1;
        }

        var savedMaterials = new List<Material>();

        foreach (var file in files)
        {
            var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            var material = new Material
            {
                Id = $"M{nextNumber:D3}",
                CourseId = courseId,
                Title = title,
                FileUrl = "/uploads/" + fileName,
                MaterialType = Path.GetExtension(file.FileName).TrimStart('.')
            };

            _db.Materials.Add(material);
            savedMaterials.Add(material);
            nextNumber++;
        }

        _db.SaveChanges();
        return Json(savedMaterials);
    }

    // =======================
    // GET MATERIALS (AJAX)
    // =======================
    [HttpGet]
    public IActionResult GetMaterials(string courseId)
    {
        var materials = _db.Materials
            .Where(m => m.CourseId == courseId)
            .OrderByDescending(m => m.Id)
            .Select(m => new { m.Id, m.Title, m.FileUrl })
            .ToList();

        return Json(materials);
    }

    // =======================
    // DELETE MATERIAL
    // =======================
    [HttpPost]
    public IActionResult DeleteMaterial(string id)
    {
        var material = _db.Materials.FirstOrDefault(m => m.Id == id);
        if (material != null)
        {
            var filePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                material.FileUrl.TrimStart('/')
            );

            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            _db.Materials.Remove(material);
            _db.SaveChanges();
        }

        return Ok();
    }

    // =======================
    // STUDENT CLASSWORK PAGE
    // =======================
    public IActionResult StudentClasswork(string courseId)
    {
        if (string.IsNullOrEmpty(courseId))
            return BadRequest("Course ID is required.");

        var materials = _db.Materials
            .Where(m => m.CourseId == courseId)
            .OrderBy(m => m.Title)
            .ThenBy(m => m.Id)
            .ToList();

        ViewBag.CourseId = courseId.ToUpper(); 

        return View("~/Views/Student/Classwork.cshtml", materials);
    }

    // =======================
    // STUDENT CLASSROOM UI
    // =======================
    public IActionResult ClassroomUI()
    {
        var courses = _db.Courses
            .OrderBy(c => c.Id)
            .ToList();

        return View("~/Views/Student/ClassroomUI.cshtml", courses);
    }
    // =======================
    // STAFF CLASSROOM UI
    // =======================
    public IActionResult StaffClassroomUI()
    {
        var courses = _db.Courses
            .OrderBy(c => c.Id)
            .ToList();

        return View("~/Views/Staff/ClassroomUI.cshtml", courses);
    }

}
