using Microsoft.EntityFrameworkCore;
using pos_backend.Models;

namespace pos_backend.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(PosDbContext context)
    {
        await context.Database.MigrateAsync();

        if (await context.Categories.AnyAsync())
        {
            return; // DB has already been seeded
        }

        var categories = new List<Category>
        {
            new() { Name = "Hot Beverages", Description = "Freshly brewed hot coffees and teas", Icon = "coffee", SortOrder = 1 },
            new() { Name = "Cold Beverages", Description = "Iced coffees, blends, and sodas", Icon = "cup-soda", SortOrder = 2 },
            new() { Name = "Main Dishes", Description = "Burgers, sandwiches, and pasta", Icon = "utensils", SortOrder = 3 },
            new() { Name = "Sides & Snacks", Description = "Fries, nuggets, and finger food", Icon = "popcorn", SortOrder = 4 },
            new() { Name = "Desserts", Description = "Cakes, waffles, and pastries", Icon = "cake", SortOrder = 5 }
        };

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        var products = new List<Product>
        {
            new()
            {
                Name = "Espresso",
                Description = "Rich double shot espresso",
                Sku = "BEV-ESP-01",
                Barcode = "100001",
                Price = 6.50m,
                CostPrice = 2.00m,
                StockQuantity = 100,
                CategoryId = categories[0].Id,
                ImageUrl = "https://images.unsplash.com/photo-1510591509098-f4fdc6d0ff04?w=300",
                ModifiersJson = """[{"name":"Sugar","options":[{"name":"No Sugar","price":0},{"name":"Less Sugar","price":0},{"name":"Normal","price":0}]},{"name":"Milk","options":[{"name":"Oat Milk","price":2.0},{"name":"Almond Milk","price":2.0}]}]"""
            },
            new()
            {
                Name = "Americano",
                Description = "Espresso with hot water",
                Sku = "BEV-AME-01",
                Barcode = "100002",
                Price = 8.00m,
                CostPrice = 2.20m,
                StockQuantity = 100,
                CategoryId = categories[0].Id,
                ImageUrl = "https://images.unsplash.com/photo-1551030173-122aabc4489c?w=300"
            },
            new()
            {
                Name = "Caffe Latte",
                Description = "Espresso with steamed silky milk",
                Sku = "BEV-LAT-01",
                Barcode = "100003",
                Price = 11.00m,
                CostPrice = 3.50m,
                StockQuantity = 80,
                CategoryId = categories[0].Id,
                ImageUrl = "https://images.unsplash.com/photo-1570968915860-54d5c301fa9f?w=300",
                ModifiersJson = """[{"name":"Extra Shot","options":[{"name":"Single Shot","price":2.5}]},{"name":"Syrup","options":[{"name":"Vanilla","price":1.5},{"name":"Caramel","price":1.5},{"name":"Hazelnut","price":1.5}]}]"""
            },
            new()
            {
                Name = "Iced Caramel Macchiato",
                Description = "Espresso poured over vanilla milk and caramel drizzle",
                Sku = "BEV-ICM-01",
                Barcode = "100004",
                Price = 14.50m,
                CostPrice = 4.50m,
                StockQuantity = 60,
                CategoryId = categories[1].Id,
                ImageUrl = "https://images.unsplash.com/photo-1517701550927-30cf4ba1dba5?w=300"
            },
            new()
            {
                Name = "Iced Matcha Latte",
                Description = "Premium Japanese green tea with cold milk",
                Sku = "BEV-IML-01",
                Barcode = "100005",
                Price = 13.50m,
                CostPrice = 4.00m,
                StockQuantity = 50,
                CategoryId = categories[1].Id,
                ImageUrl = "https://images.unsplash.com/photo-1536256263959-770b48d82b0a?w=300"
            },
            new()
            {
                Name = "Classic Cheeseburger",
                Description = "Juicy beef patty, cheddar cheese, lettuce, pickles",
                Sku = "FD-CHK-01",
                Barcode = "200001",
                Price = 18.00m,
                CostPrice = 7.50m,
                StockQuantity = 40,
                CategoryId = categories[2].Id,
                ImageUrl = "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?w=300",
                ModifiersJson = """[{"name":"Patty","options":[{"name":"Single Patty","price":0},{"name":"Double Patty","price":6.0}]},{"name":"Add-ons","options":[{"name":"Extra Cheese","price":2.0},{"name":"Bacon Slice","price":3.5}]}]"""
            },
            new()
            {
                Name = "Crispy Chicken Sandwich",
                Description = "Golden fried chicken breast, spicy mayo, brioche bun",
                Sku = "FD-CCS-01",
                Barcode = "200002",
                Price = 16.50m,
                CostPrice = 6.00m,
                StockQuantity = 45,
                CategoryId = categories[2].Id,
                ImageUrl = "https://images.unsplash.com/photo-1606755962773-d324e0a13086?w=300"
            },
            new()
            {
                Name = "Truffle French Fries",
                Description = "Crispy golden fries tossed with truffle oil and parmesan",
                Sku = "FD-TFF-01",
                Barcode = "300001",
                Price = 10.50m,
                CostPrice = 3.00m,
                StockQuantity = 75,
                CategoryId = categories[3].Id,
                ImageUrl = "https://images.unsplash.com/photo-1573080496219-bb080dd4f877?w=300"
            },
            new()
            {
                Name = "Crispy Chicken Tenders (5pcs)",
                Description = "Hand-breaded tender chicken with honey mustard dip",
                Sku = "FD-CCT-01",
                Barcode = "300002",
                Price = 12.00m,
                CostPrice = 4.20m,
                StockQuantity = 50,
                CategoryId = categories[3].Id,
                ImageUrl = "https://images.unsplash.com/photo-1562967914-608f82629710?w=300"
            },
            new()
            {
                Name = "Burnt Basque Cheesecake",
                Description = "Creamy caramelized baked cheesecake slice",
                Sku = "DS-BBC-01",
                Barcode = "400001",
                Price = 14.00m,
                CostPrice = 4.50m,
                StockQuantity = 25,
                CategoryId = categories[4].Id,
                ImageUrl = "https://images.unsplash.com/photo-1533134242443-d4fd215305ad?w=300"
            },
            new()
            {
                Name = "Chocolate Fudge Brownie",
                Description = "Warm chocolate brownie served with chocolate ganache",
                Sku = "DS-CFB-01",
                Barcode = "400002",
                Price = 9.50m,
                CostPrice = 3.00m,
                StockQuantity = 30,
                CategoryId = categories[4].Id,
                ImageUrl = "https://images.unsplash.com/photo-1606313564200-e75d5e30476c?w=300"
            }
        };

        await context.Products.AddRangeAsync(products);
        await context.SaveChangesAsync();
    }
}
