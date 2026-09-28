using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pos_backend.Data;
using pos_backend.DTOs;
using pos_backend.Models;

namespace pos_backend.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly PosDbContext _context;

    public ProductsController(PosDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts(
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        [FromQuery] bool activeOnly = false)
    {
        var query = _context.Products
            .Include(p => p.Category)
            .AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (activeOnly)
        {
            query = query.Where(p => p.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var cleanSearch = search.Trim().ToLower();
            query = query.Where(p =>
                p.Name.ToLower().Contains(cleanSearch) ||
                (p.Sku != null && p.Sku.ToLower().Contains(cleanSearch)) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(cleanSearch)));
        }

        var products = await query
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.Name)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Sku,
                p.Barcode,
                p.Price,
                p.CostPrice,
                p.StockQuantity,
                p.TrackStock,
                p.ImageUrl,
                p.ModifiersJson,
                p.IsActive,
                p.CategoryId,
                p.Category != null ? p.Category.Name : null
            ))
            .ToListAsync();

        return Ok(products);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetProduct(int id)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Id == id)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Sku,
                p.Barcode,
                p.Price,
                p.CostPrice,
                p.StockQuantity,
                p.TrackStock,
                p.ImageUrl,
                p.ModifiersJson,
                p.IsActive,
                p.CategoryId,
                p.Category != null ? p.Category.Name : null
            ))
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} not found." });
        }

        return Ok(product);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<ProductDto>> GetProductByBarcode(string barcode)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Barcode == barcode && p.IsActive)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Description,
                p.Sku,
                p.Barcode,
                p.Price,
                p.CostPrice,
                p.StockQuantity,
                p.TrackStock,
                p.ImageUrl,
                p.ModifiersJson,
                p.IsActive,
                p.CategoryId,
                p.Category != null ? p.Category.Name : null
            ))
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return NotFound(new { message = $"Product with barcode '{barcode}' not found." });
        }

        return Ok(product);
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> CreateProduct([FromBody] CreateProductRequest request)
    {
        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            return BadRequest(new { message = $"Category with ID {request.CategoryId} does not exist." });
        }

        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Sku = request.Sku,
            Barcode = request.Barcode,
            Price = request.Price,
            CostPrice = request.CostPrice,
            StockQuantity = request.StockQuantity,
            TrackStock = request.TrackStock,
            ImageUrl = request.ImageUrl,
            ModifiersJson = request.ModifiersJson,
            IsActive = request.IsActive,
            CategoryId = request.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var category = await _context.Categories.FindAsync(product.CategoryId);

        var result = new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Sku,
            product.Barcode,
            product.Price,
            product.CostPrice,
            product.StockQuantity,
            product.TrackStock,
            product.ImageUrl,
            product.ModifiersJson,
            product.IsActive,
            product.CategoryId,
            category?.Name
        );

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductRequest request)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} not found." });
        }

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
        {
            return BadRequest(new { message = $"Category with ID {request.CategoryId} does not exist." });
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Sku = request.Sku;
        product.Barcode = request.Barcode;
        product.Price = request.Price;
        product.CostPrice = request.CostPrice;
        product.StockQuantity = request.StockQuantity;
        product.TrackStock = request.TrackStock;
        product.ImageUrl = request.ImageUrl;
        product.ModifiersJson = request.ModifiersJson;
        product.IsActive = request.IsActive;
        product.CategoryId = request.CategoryId;
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPatch("{id:int}/stock")]
    public async Task<ActionResult<ProductDto>> AdjustStock(int id, [FromBody] AdjustStockRequest request)
    {
        var product = await _context.Products.Include(p => p.Category).FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} not found." });
        }

        product.StockQuantity += request.QuantityAdjustment;
        if (product.StockQuantity < 0)
        {
            product.StockQuantity = 0;
        }
        product.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new ProductDto(
            product.Id,
            product.Name,
            product.Description,
            product.Sku,
            product.Barcode,
            product.Price,
            product.CostPrice,
            product.StockQuantity,
            product.TrackStock,
            product.ImageUrl,
            product.ModifiersJson,
            product.IsActive,
            product.CategoryId,
            product.Category?.Name
        ));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound(new { message = $"Product with ID {id} not found." });
        }

        // Soft delete
        product.IsActive = false;
        product.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

