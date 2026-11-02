using CoffeeShop.Data;
using CoffeeShop.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Controllers;

[AllowAnonymous]
public class CatalogController : Controller
{
    private readonly AppDbContext _db;

    public CatalogController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("/menu")]
    public async Task<IActionResult> Menu(
        string? keyword = null,
        int? categoryId = null,
        decimal? minPrice = null,
        decimal? maxPrice = null,
        bool onlyAvailable = false,
        string sort = "popular",
        int page = 1)
    {
        page = Math.Max(1, page);
        const int pageSize = 9;

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted && c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Sizes)
            .Where(p => !p.IsDeleted && p.Category.IsActive && !p.Category.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim();
            query = query.Where(p => p.Name.Contains(kw) || (p.Description != null && p.Description.Contains(kw)));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (minPrice.HasValue)
        {
            query = query.Where(p => p.BasePrice >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(p => p.BasePrice <= maxPrice.Value);
        }

        if (onlyAvailable)
        {
            query = query.Where(p => p.IsAvailable);
        }

        query = sort switch
        {
            "price_asc" => query.OrderBy(p => p.BasePrice).ThenBy(p => p.Name),
            "price_desc" => query.OrderByDescending(p => p.BasePrice).ThenBy(p => p.Name),
            "newest" => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id),
            _ => query.OrderByDescending(p => p.IsFeatured).ThenByDescending(p => p.SoldCount).ThenBy(p => p.Id)
        };

        var totalItems = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var cardItems = items.Select(p =>
        {
            var firstSize = p.Sizes.OrderBy(s => s.DisplayOrder).FirstOrDefault();
            return new ProductCardVm
            {
                Id = p.Id,
                Name = p.Name,
                CategoryName = p.Category.Name,
                Description = p.Description,
                ImageUrl = p.ImageUrl,
                DisplayPrice = firstSize?.Price ?? p.BasePrice,
                DefaultSizeName = firstSize?.SizeName,
                IsAvailable = p.IsAvailable,
                IsFeatured = p.IsFeatured,
                SoldCount = p.SoldCount,
                LiquidColorHex = p.LiquidColorHex
            };
        }).ToList();

        return View(new MenuFilterVm
        {
            Keyword = keyword,
            CategoryId = categoryId,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            OnlyAvailable = onlyAvailable,
            Sort = sort,
            Page = page,
            Categories = categories,
            Products = new PagedResult<ProductCardVm>
            {
                Items = cardItems,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            }
        });
    }

    [HttpGet("/product/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Sizes)
            .Include(p => p.ProductToppings)
                .ThenInclude(pt => pt.Topping)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted && !p.Category.IsDeleted && p.Category.IsActive);

        if (product == null)
            return NotFound();

        var relatedRaw = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Sizes)
            .Where(p => !p.IsDeleted && p.CategoryId == product.CategoryId && p.Id != product.Id)
            .OrderByDescending(p => p.IsAvailable)
            .ThenByDescending(p => p.SoldCount)
            .Take(4)
            .ToListAsync();

        var related = relatedRaw.Select(p =>
        {
            var fs = p.Sizes.OrderBy(s => s.DisplayOrder).FirstOrDefault();
            return new ProductCardVm
            {
                Id = p.Id,
                Name = p.Name,
                CategoryName = p.Category.Name,
                Description = p.Description,
                ImageUrl = p.ImageUrl,
                DisplayPrice = fs?.Price ?? p.BasePrice,
                DefaultSizeName = fs?.SizeName,
                IsAvailable = p.IsAvailable,
                IsFeatured = p.IsFeatured,
                SoldCount = p.SoldCount,
                LiquidColorHex = p.LiquidColorHex
            };
        }).ToList();

        var vm = new ProductDetailVm
        {
            Id = product.Id,
            Name = product.Name,
            CategoryName = product.Category.Name,
            Description = product.Description,
            ImageUrl = product.ImageUrl,
            BasePrice = product.BasePrice,
            IsAvailable = product.IsAvailable,
            AllowTemperature = product.AllowTemperature,
            AllowSugar = product.AllowSugar,
            AllowIce = product.AllowIce,
            LiquidColorHex = product.LiquidColorHex,
            Sizes = product.Sizes.OrderBy(s => s.DisplayOrder).Select(s => (s.SizeName, s.Price)).ToList(),
            Toppings = product.ProductToppings
                .Select(pt => pt.Topping)
                .Where(t => !t.IsDeleted && t.IsActive)
                .OrderBy(t => t.Price)
                .Select(t => (t.Id, t.Name, t.Price))
                .ToList(),
            RelatedProducts = related
        };

        return View(vm);
    }
}

