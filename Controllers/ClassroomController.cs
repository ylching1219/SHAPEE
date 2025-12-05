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

    // Load classwork view
    public IActionResult Classwork(string id)
    {
        var room = _db.Courses.FirstOrDefault(c => c.Id == id);
        ViewBag.Room = room;
        return View("~/Views/Staff/Classwork.cshtml");
    }

    // Upload multiple files with single title
    [HttpPost]
    public IActionResult UploadDoc(string courseId, string title, IFormFile[] files)
    {
        if (files == null || files.Length == 0)
            return BadRequest("No files selected.");
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest("Title is required.");

        var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        if (!Directory.Exists(uploadFolder)) Directory.CreateDirectory(uploadFolder);

        // Get the current max material number
        var lastMaterial = _db.Materials
            .OrderByDescending(m => m.Id)
            .FirstOrDefault();

        int nextNumber = 1;
        if (lastMaterial != null && lastMaterial.Id.StartsWith("M"))
        {
            // Extract the number part of the ID
            if (int.TryParse(lastMaterial.Id.Substring(1), out int num))
            {
                nextNumber = num + 1;
            }
        }

        var savedMaterials = new List<Material>();

        foreach (var file in files)
        {
            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var filePath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                file.CopyTo(stream);

            var material = new Material
            {
                Id = $"M{nextNumber:D3}", // M001, M002, etc.
                CourseId = courseId,
                Title = title,
                FileUrl = "/uploads/" + fileName,
                MaterialType = Path.GetExtension(file.FileName).TrimStart('.')
            };

            _db.Materials.Add(material);
            savedMaterials.Add(material);
            nextNumber++; // increment for next file
        }

        _db.SaveChanges();
        return Json(savedMaterials);
    }

    // Get all materials for a course
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

    // Delete a single material
    [HttpPost]
    public IActionResult DeleteMaterial(string id)
    {
        var material = _db.Materials.FirstOrDefault(m => m.Id == id);
        if (material != null)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", material.FileUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);

            _db.Materials.Remove(material);
            _db.SaveChanges();
        }
        return Ok();
    }

    // Clear all materials for a course
    [HttpPost]
    public IActionResult ClearAllMaterials(string courseId)
    {
        var materials = _db.Materials.Where(m => m.CourseId == courseId).ToList();
        foreach (var material in materials)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", material.FileUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        }

        _db.Materials.RemoveRange(materials);
        _db.SaveChanges();
        return Ok();
    }
}
