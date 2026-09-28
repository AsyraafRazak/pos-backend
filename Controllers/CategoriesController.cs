using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pos_backend.Data;
using pos_backend.DTOs;
using pos_backend.Models;

namespace pos_backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly PosDbContext _context;

    public CategoriesController(PosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetCategories([FromQuery] bool activeOnly = false)
    {
        var query = _context.Categories.AsQueryable();

        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        var categories = await query
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.Icon,
                c.SortOrder,
                c.IsActive,
                c.Products.Count(p => !activeOnly || p.IsActive)
            ))
            .ToListAsync();

        return Ok(categories);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id)
    {
        var category = await _context.Categories
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.Icon,
                c.SortOrder,
                c.IsActive,
                c.Products.Count
            ))
            .FirstOrDefaultAsync();

        if (category == null)
        {
            return NotFound(new { message = $"Category with ID {id} not found." });
        }

        return Ok(category);
    }

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var category = new Category
        {
            Name = request.Name,
            Description = request.Description,
            Icon = request.Icon,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        var result = new CategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.Icon,
            category.SortOrder,
            category.IsActive,
            0
        );

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateCategoryRequest request)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound(new { message = $"Category with ID {id} not found." });
        }

        category.Name = request.Name;
        category.Description = request.Description;
        category.Icon = request.Icon;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (category == null)
        {
            return NotFound(new { message = $"Category with ID {id} not found." });
        }

        if (category.Products.Any())
        {
            return BadRequest(new { message = "Cannot delete category containing products. Reassign or delete products first." });
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

