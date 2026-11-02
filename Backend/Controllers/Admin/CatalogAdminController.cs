using System.Text.RegularExpressions;
using CoffeeShop.Areas.Admin.Models;
using CoffeeShop.Authorization;
using CoffeeShop.Data;
using CoffeeShop.Helpers;
using CoffeeShop.Models.Entities;
using CoffeeShop.Models.ViewModels;
using CoffeeShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class CatalogAdminController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly IAuditService _auditService;

    public CatalogAdminController(AppDbContext db, IWebHostEnvironment env, IAuditService auditService)
    {
        _db = db;
        _env = env;
        _auditService = auditService;
    }

    // =========================================================================
    // 1. CATEGORIES (/admin/categories)
    // =========================================================================
    [HttpGet("/admin/categories")]
    public async Task<IActionResult> Categories(int? editId = null)
    {
        var items = await _db.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryListItemVm
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                ImageUrl = c.ImageUrl,
                IconClass = c.IconClass,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
                ProductCount = c.Products.Count(p => !p.IsDeleted)
            })
            .ToListAsync();

        var form = new CategoryFormModel();
        if (editId.HasValue)
        {
            var target = await _db.Categories.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == editId.Value && !c.IsDeleted);
            if (target != null)
            {
                form = new CategoryFormModel
                {
                    Id = target.Id,
                    Name = target.Name,
                    Slug = target.Slug,
                    IconClass = target.IconClass,
                    DisplayOrder = target.DisplayOrder,
                    IsActive = target.IsActive,
                    ExistingImageUrl = target.ImageUrl
                };
            }
        }

        return View("Categories", new CategoriesAdminViewModel
        {
            Categories = items,
            Form = form
        });
    }

    [HttpPost("/admin/categories/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(CategoryFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Name))
        {
            TempData["ToastError"] = "Vui lòng nhập tên danh mục.";
            return RedirectToAction(nameof(Categories));
        }

        var cleanName = form.Name.Trim();
        var baseSlug = FormatHelper.Slugify(string.IsNullOrWhiteSpace(form.Slug) ? cleanName : form.Slug);
        var uniqueSlug = await EnsureUniqueCategorySlugAsync(baseSlug, form.Id);
        var icon = string.IsNullOrWhiteSpace(form.IconClass) ? "bi-cup-hot" : form.IconClass.Trim();

        if (form.Id == 0)
        {
            string? imageUrl = null;
            if (form.ImageFile != null && form.ImageFile.Length > 0)
            {
                var up = await ImageUploadHelper.SaveImageAsync(form.ImageFile, _env, "categories");
                if (!up.Success)
                {
                    TempData["ToastError"] = up.ErrorMessage;
                    return RedirectToAction(nameof(Categories));
                }
                imageUrl = up.RelativeUrl;
            }

            var cat = new Category
            {
                Name = cleanName,
                Slug = uniqueSlug,
                IconClass = icon,
                ImageUrl = imageUrl,
                DisplayOrder = form.DisplayOrder,
                IsActive = form.IsActive,
                IsDeleted = false
            };

            _db.Categories.Add(cat);
            await _db.SaveChangesAsync();

            await _auditService.LogAsync("CreateCategory", "Category", cat.Id.ToString(), null,
                new { cat.Name, cat.Slug, cat.DisplayOrder, cat.IsActive });

            TempData["ToastSuccess"] = $"Đã tạo danh mục '{cat.Name}'.";
        }
        else
        {
            var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == form.Id && !c.IsDeleted);
            if (cat == null)
            {
                TempData["ToastError"] = "Không tìm thấy danh mục cần cập nhật.";
                return RedirectToAction(nameof(Categories));
            }

            if (form.ImageFile != null && form.ImageFile.Length > 0)
            {
                var up = await ImageUploadHelper.SaveImageAsync(form.ImageFile, _env, "categories", cat.ImageUrl);
                if (!up.Success)
                {
                    TempData["ToastError"] = up.ErrorMessage;
                    return RedirectToAction(nameof(Categories), new { editId = cat.Id });
                }
                cat.ImageUrl = up.RelativeUrl;
            }

            var oldSnap = new { cat.Name, cat.Slug, cat.DisplayOrder, cat.IsActive };
            cat.Name = cleanName;
            cat.Slug = uniqueSlug;
            cat.IconClass = icon;
            cat.DisplayOrder = form.DisplayOrder;
            cat.IsActive = form.IsActive;

            await _db.SaveChangesAsync();

            await _auditService.LogAsync("UpdateCategory", "Category", cat.Id.ToString(), oldSnap,
                new { cat.Name, cat.Slug, cat.DisplayOrder, cat.IsActive });

            TempData["ToastSuccess"] = $"Đã cập nhật danh mục '{cat.Name}'.";
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost("/admin/categories/toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCategory(int id)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (cat == null)
        {
            TempData["ToastError"] = "Không tìm thấy danh mục.";
            return RedirectToAction(nameof(Categories));
        }

        cat.IsActive = !cat.IsActive;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("ToggleCategory", "Category", cat.Id.ToString(),
            new { IsActive = !cat.IsActive }, new { cat.IsActive });

        TempData["ToastSuccess"] = $"Danh mục '{cat.Name}' đã chuyển sang {(cat.IsActive ? "Hiển thị" : "Tạm ẩn")}.";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost("/admin/categories/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var cat = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
        if (cat == null)
        {
            TempData["ToastError"] = "Không tìm thấy danh mục.";
            return RedirectToAction(nameof(Categories));
        }

        // Quy tắc Mục 7.3: CHẶN xóa danh mục đang còn sản phẩm chưa xóa
        var activeProductCount = await _db.Products.CountAsync(p => p.CategoryId == id && !p.IsDeleted);
        if (activeProductCount > 0)
        {
            TempData["ToastError"] = $"Không thể xóa danh mục '{cat.Name}' vì đang có {activeProductCount} sản phẩm thuộc danh mục này. Vui lòng chuyển sản phẩm sang danh mục khác hoặc chọn 'Tạm ẩn' danh mục.";
            return RedirectToAction(nameof(Categories));
        }

        cat.IsDeleted = true;
        cat.IsActive = false;
        cat.Slug = $"{cat.Slug}-del-{cat.Id}";
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("DeleteCategory", "Category", cat.Id.ToString(),
            new { cat.Name }, new { IsDeleted = true });

        TempData["ToastSuccess"] = $"Đã xóa danh mục '{cat.Name}'.";
        return RedirectToAction(nameof(Categories));
    }

    private async Task<string> EnsureUniqueCategorySlugAsync(string baseSlug, int excludeId)
    {
        var candidate = baseSlug;
        int suffix = 2;
        while (await _db.Categories.AnyAsync(c => c.Slug == candidate && c.Id != excludeId))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }
        return candidate;
    }

    // =========================================================================
    // 2. TOPPINGS (/admin/toppings)
    // =========================================================================
    [HttpGet("/admin/toppings")]
    public async Task<IActionResult> Toppings(int? editId = null)
    {
        var toppings = await _db.Toppings
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var form = new ToppingFormModel();
        if (editId.HasValue)
        {
            var target = toppings.FirstOrDefault(t => t.Id == editId.Value);
            if (target != null)
            {
                form = new ToppingFormModel
                {
                    Id = target.Id,
                    Name = target.Name,
                    Price = target.Price,
                    IsActive = target.IsActive
                };
            }
        }

        return View("Toppings", new ToppingsAdminViewModel
        {
            Toppings = toppings,
            Form = form
        });
    }

    [HttpPost("/admin/toppings/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTopping(ToppingFormModel form)
    {
        if (string.IsNullOrWhiteSpace(form.Name) || form.Price <= 0)
        {
            TempData["ToastError"] = "Tên topping không được để trống và giá phải lớn hơn 0 ₫.";
            return RedirectToAction(nameof(Toppings));
        }

        var cleanName = form.Name.Trim();

        if (form.Id == 0)
        {
            var top = new Topping
            {
                Name = cleanName,
                Price = Math.Round(form.Price, 0),
                IsActive = form.IsActive,
                IsDeleted = false
            };
            _db.Toppings.Add(top);
            await _db.SaveChangesAsync();

            await _auditService.LogAsync("CreateTopping", "Topping", top.Id.ToString(), null,
                new { top.Name, top.Price, top.IsActive });

            TempData["ToastSuccess"] = $"Đã thêm topping '{top.Name}' ({FormatHelper.Vnd(top.Price)}).";
        }
        else
        {
            var top = await _db.Toppings.FirstOrDefaultAsync(t => t.Id == form.Id && !t.IsDeleted);
            if (top == null)
            {
                TempData["ToastError"] = "Không tìm thấy topping cần sửa.";
                return RedirectToAction(nameof(Toppings));
            }

            var oldSnap = new { top.Name, top.Price, top.IsActive };
            top.Name = cleanName;
            top.Price = Math.Round(form.Price, 0);
            top.IsActive = form.IsActive;
            await _db.SaveChangesAsync();

            await _auditService.LogAsync("UpdateTopping", "Topping", top.Id.ToString(), oldSnap,
                new { top.Name, top.Price, top.IsActive });

            TempData["ToastSuccess"] = $"Đã cập nhật topping '{top.Name}'.";
        }

        return RedirectToAction(nameof(Toppings));
    }

    [HttpPost("/admin/toppings/toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleTopping(int id)
    {
        var top = await _db.Toppings.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (top == null)
        {
            TempData["ToastError"] = "Không tìm thấy topping.";
            return RedirectToAction(nameof(Toppings));
        }

        top.IsActive = !top.IsActive;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("ToggleTopping", "Topping", top.Id.ToString(),
            new { IsActive = !top.IsActive }, new { top.IsActive });

        TempData["ToastSuccess"] = $"Topping '{top.Name}' đã chuyển sang {(top.IsActive ? "Đang bán" : "Tạm ngưng")}.";
        return RedirectToAction(nameof(Toppings));
    }

    [HttpPost("/admin/toppings/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTopping(int id)
    {
        var top = await _db.Toppings.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);
        if (top == null)
        {
            TempData["ToastError"] = "Không tìm thấy topping.";
            return RedirectToAction(nameof(Toppings));
        }

        top.IsDeleted = true;
        top.IsActive = false;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("DeleteTopping", "Topping", top.Id.ToString(),
            new { top.Name, top.Price }, new { IsDeleted = true });

        TempData["ToastSuccess"] = $"Đã xóa topping '{top.Name}'.";
        return RedirectToAction(nameof(Toppings));
    }

    // =========================================================================
    // 3. PRODUCTS & RECIPES (/admin/products)
    // =========================================================================
    [HttpGet("/admin/products")]
    public async Task<IActionResult> Products(string? search = null, int? categoryId = null, bool? isAvailable = null, int page = 1)
    {
        const int pageSize = 10;
        page = Math.Max(1, page);

        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Sizes)
            .Include(p => p.Recipes)
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            query = query.Where(p => p.Name.Contains(kw) || (p.Description != null && p.Description.Contains(kw)));
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (isAvailable.HasValue)
        {
            query = query.Where(p => p.IsAvailable == isAvailable.Value);
        }

        var totalItems = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.IsFeatured)
            .ThenBy(p => p.CategoryId)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        var qsParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) qsParts.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (categoryId.HasValue) qsParts.Add($"categoryId={categoryId.Value}");
        if (isAvailable.HasValue) qsParts.Add($"isAvailable={isAvailable.Value.ToString().ToLowerInvariant()}");

        var baseQueryUrl = qsParts.Count > 0
            ? $"/admin/products?{string.Join("&", qsParts)}"
            : "/admin/products";

        var vm = new ProductsAdminViewModel
        {
            Search = search,
            CategoryId = categoryId,
            IsAvailable = isAvailable,
            Categories = categories,
            BaseQueryUrl = baseQueryUrl,
            PagedProducts = new PagedResult<Product>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems
            }
        };

        return View("Products", vm);
    }

    [HttpGet("/admin/products/create")]
    public async Task<IActionResult> CreateProduct()
    {
        var vm = new ProductFormViewModel
        {
            Sizes = new List<ProductSizeRowVm>
            {
                new() { SizeName = "S", Price = 35000, DisplayOrder = 1 },
                new() { SizeName = "M", Price = 42000, DisplayOrder = 2 },
                new() { SizeName = "L", Price = 49000, DisplayOrder = 3 }
            }
        };
        await PopulateProductFormLookupsAsync(vm);
        return View("ProductForm", vm);
    }

    [HttpGet("/admin/products/edit/{id:int}")]
    public async Task<IActionResult> EditProduct(int id)
    {
        var prod = await _db.Products
            .AsNoTracking()
            .Include(p => p.Sizes)
            .Include(p => p.ProductToppings)
            .Include(p => p.Recipes)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        if (prod == null)
        {
            TempData["ToastError"] = "Không tìm thấy sản phẩm.";
            return RedirectToAction(nameof(Products));
        }

        var vm = new ProductFormViewModel
        {
            Id = prod.Id,
            Name = prod.Name,
            CategoryId = prod.CategoryId,
            Description = prod.Description,
            BasePrice = prod.BasePrice,
            ExistingImageUrl = prod.ImageUrl,
            LiquidColorHex = string.IsNullOrWhiteSpace(prod.LiquidColorHex) ? "#4A2C18" : prod.LiquidColorHex,
            AllowTemperature = prod.AllowTemperature,
            AllowSugar = prod.AllowSugar,
            AllowIce = prod.AllowIce,
            IsFeatured = prod.IsFeatured,
            IsAvailable = prod.IsAvailable,
            Sizes = prod.Sizes
                .OrderBy(s => s.DisplayOrder)
                .ThenBy(s => s.Price)
                .Select(s => new ProductSizeRowVm
                {
                    SizeName = s.SizeName,
                    Price = s.Price,
                    DisplayOrder = s.DisplayOrder
                })
                .ToList(),
            SelectedToppingIds = prod.ProductToppings.Select(pt => pt.ToppingId).ToList(),
            Recipes = prod.Recipes
                .OrderBy(r => r.SizeName)
                .ThenBy(r => r.IngredientId)
                .Select(r => new ProductRecipeRowVm
                {
                    IngredientId = r.IngredientId,
                    SizeName = r.SizeName,
                    Quantity = r.Quantity
                })
                .ToList()
        };

        await PopulateProductFormLookupsAsync(vm);
        return View("ProductForm", vm);
    }

    [HttpPost("/admin/products/save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProduct(ProductFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await PopulateProductFormLookupsAsync(vm);
            return View("ProductForm", vm);
        }

        var categoryExists = await _db.Categories.AnyAsync(c => c.Id == vm.CategoryId && !c.IsDeleted);
        if (!categoryExists)
        {
            ModelState.AddModelError(nameof(vm.CategoryId), "Danh mục đã chọn không tồn tại.");
            await PopulateProductFormLookupsAsync(vm);
            return View("ProductForm", vm);
        }

        var hex = (vm.LiquidColorHex ?? "#4A2C18").Trim();
        if (!Regex.IsMatch(hex, @"^#[0-9A-Fa-f]{6}$"))
        {
            hex = "#4A2C18";
        }

        // Lọc và chuẩn hóa danh sách Size
        var validSizes = (vm.Sizes ?? new List<ProductSizeRowVm>())
            .Where(s => !string.IsNullOrWhiteSpace(s.SizeName) && s.Price > 0)
            .GroupBy(s => s.SizeName.Trim().ToUpperInvariant())
            .Select((g, idx) =>
            {
                var first = g.First();
                return new ProductSize
                {
                    SizeName = g.Key,
                    Price = Math.Round(first.Price, 0),
                    DisplayOrder = first.DisplayOrder > 0 ? first.DisplayOrder : idx + 1
                };
            })
            .ToList();

        // Lọc và chuẩn hóa danh sách Công thức định lượng (Recipe)
        var validIngredientIds = await _db.Ingredients
            .Where(i => !i.IsDeleted)
            .Select(i => i.Id)
            .ToHashSetAsync();

        var validRecipes = (vm.Recipes ?? new List<ProductRecipeRowVm>())
            .Where(r => r.IngredientId > 0 && r.Quantity > 0 && validIngredientIds.Contains(r.IngredientId))
            .GroupBy(r => new
            {
                r.IngredientId,
                SizeKey = string.IsNullOrWhiteSpace(r.SizeName) ? "" : r.SizeName.Trim().ToUpperInvariant()
            })
            .Select(g => new Recipe
            {
                IngredientId = g.Key.IngredientId,
                SizeName = string.IsNullOrEmpty(g.Key.SizeKey) ? null : g.Key.SizeKey,
                Quantity = Math.Round(g.Last().Quantity, 2)
            })
            .ToList();

        // Lọc danh sách Topping hợp lệ
        var validToppingIds = await _db.Toppings
            .Where(t => !t.IsDeleted)
            .Select(t => t.Id)
            .ToHashSetAsync();

        var chosenToppings = (vm.SelectedToppingIds ?? new List<int>())
            .Distinct()
            .Where(id => validToppingIds.Contains(id))
            .ToList();

        if (vm.Id == 0)
        {
            string imageUrl = "/images/seed/default-drink.svg";
            if (vm.ImageFile != null && vm.ImageFile.Length > 0)
            {
                var up = await ImageUploadHelper.SaveImageAsync(vm.ImageFile, _env, "products");
                if (!up.Success)
                {
                    ModelState.AddModelError(nameof(vm.ImageFile), up.ErrorMessage ?? "Lỗi tải ảnh.");
                    await PopulateProductFormLookupsAsync(vm);
                    return View("ProductForm", vm);
                }
                imageUrl = up.RelativeUrl!;
            }

            var prod = new Product
            {
                Name = vm.Name.Trim(),
                CategoryId = vm.CategoryId,
                Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
                BasePrice = Math.Round(vm.BasePrice, 0),
                ImageUrl = imageUrl,
                LiquidColorHex = hex,
                AllowTemperature = vm.AllowTemperature,
                AllowSugar = vm.AllowSugar,
                AllowIce = vm.AllowIce,
                IsFeatured = vm.IsFeatured,
                IsAvailable = vm.IsAvailable,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var sz in validSizes) prod.Sizes.Add(sz);
            foreach (var tid in chosenToppings) prod.ProductToppings.Add(new ProductTopping { ToppingId = tid });
            foreach (var rc in validRecipes) prod.Recipes.Add(rc);

            _db.Products.Add(prod);
            await _db.SaveChangesAsync();

            await _auditService.LogAsync("CreateProduct", "Product", prod.Id.ToString(), null, new
            {
                prod.Name,
                prod.BasePrice,
                Sizes = validSizes.Select(s => $"{s.SizeName}:{s.Price}"),
                RecipeCount = validRecipes.Count
            });

            TempData["ToastSuccess"] = $"Đã thêm sản phẩm mới '{prod.Name}'.";
            if (validRecipes.Count == 0)
            {
                TempData["ToastWarning"] = $"Lưu ý: Sản phẩm '{prod.Name}' hiện chưa có công thức nguyên liệu (sẽ không tự trừ kho khi pha chế).";
            }
        }
        else
        {
            var prod = await _db.Products
                .Include(p => p.Sizes)
                .Include(p => p.ProductToppings)
                .Include(p => p.Recipes)
                .FirstOrDefaultAsync(p => p.Id == vm.Id && !p.IsDeleted);

            if (prod == null)
            {
                TempData["ToastError"] = "Không tìm thấy sản phẩm cần cập nhật.";
                return RedirectToAction(nameof(Products));
            }

            if (vm.ImageFile != null && vm.ImageFile.Length > 0)
            {
                var up = await ImageUploadHelper.SaveImageAsync(vm.ImageFile, _env, "products", prod.ImageUrl);
                if (!up.Success)
                {
                    ModelState.AddModelError(nameof(vm.ImageFile), up.ErrorMessage ?? "Lỗi tải ảnh.");
                    vm.ExistingImageUrl = prod.ImageUrl;
                    await PopulateProductFormLookupsAsync(vm);
                    return View("ProductForm", vm);
                }
                prod.ImageUrl = up.RelativeUrl!;
            }

            var oldBasePrice = prod.BasePrice;
            var oldSizesSignature = string.Join(", ", prod.Sizes.OrderBy(s => s.DisplayOrder).Select(s => $"{s.SizeName}:{s.Price:0}"));
            var newBasePrice = Math.Round(vm.BasePrice, 0);
            var newSizesSignature = string.Join(", ", validSizes.OrderBy(s => s.DisplayOrder).Select(s => $"{s.SizeName}:{s.Price:0}"));
            bool priceChanged = oldBasePrice != newBasePrice || oldSizesSignature != newSizesSignature;

            prod.Name = vm.Name.Trim();
            prod.CategoryId = vm.CategoryId;
            prod.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
            prod.BasePrice = newBasePrice;
            prod.LiquidColorHex = hex;
            prod.AllowTemperature = vm.AllowTemperature;
            prod.AllowSugar = vm.AllowSugar;
            prod.AllowIce = vm.AllowIce;
            prod.IsFeatured = vm.IsFeatured;
            prod.IsAvailable = vm.IsAvailable;

            _db.ProductSizes.RemoveRange(prod.Sizes);
            foreach (var sz in validSizes)
            {
                prod.Sizes.Add(sz);
            }

            _db.ProductToppings.RemoveRange(prod.ProductToppings);
            foreach (var tid in chosenToppings)
            {
                prod.ProductToppings.Add(new ProductTopping { ProductId = prod.Id, ToppingId = tid });
            }

            _db.Recipes.RemoveRange(prod.Recipes);
            foreach (var rc in validRecipes)
            {
                prod.Recipes.Add(rc);
            }

            await _db.SaveChangesAsync();

            if (priceChanged)
            {
                await _auditService.LogAsync(
                    action: "ChangeProductPrice",
                    entityName: "Product",
                    entityId: prod.Id.ToString(),
                    oldValues: new { prod.Name, BasePrice = oldBasePrice, Sizes = oldSizesSignature },
                    newValues: new { prod.Name, BasePrice = newBasePrice, Sizes = newSizesSignature });
            }
            else
            {
                await _auditService.LogAsync(
                    action: "UpdateProduct",
                    entityName: "Product",
                    entityId: prod.Id.ToString(),
                    oldValues: null,
                    newValues: new { prod.Name, prod.IsAvailable, RecipeCount = validRecipes.Count });
            }

            TempData["ToastSuccess"] = $"Đã lưu thay đổi sản phẩm '{prod.Name}'.";
        }

        return RedirectToAction(nameof(Products));
    }

    [HttpPost("/admin/products/toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProductAvailability(int id, string? returnUrl = null)
    {
        var prod = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (prod == null)
        {
            TempData["ToastError"] = "Không tìm thấy sản phẩm.";
            return RedirectToAction(nameof(Products));
        }

        prod.IsAvailable = !prod.IsAvailable;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("ToggleProductAvailability", "Product", prod.Id.ToString(),
            new { IsAvailable = !prod.IsAvailable }, new { prod.IsAvailable });

        TempData["ToastSuccess"] = $"Đã chuyển '{prod.Name}' sang trạng thái {(prod.IsAvailable ? "Đang kinh doanh" : "Tạm hết")}.";

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Products));
    }

    [HttpPost("/admin/products/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var prod = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
        if (prod == null)
        {
            TempData["ToastError"] = "Không tìm thấy sản phẩm.";
            return RedirectToAction(nameof(Products));
        }

        // Quy tắc Mục 7.3: Nếu sản phẩm đã phát sinh trong OrderItems -> ẩn kinh doanh / soft-delete an toàn không xóa cứng
        var hasOrderItems = await _db.OrderItems.AnyAsync(oi => oi.ProductId == id);
        if (hasOrderItems)
        {
            prod.IsAvailable = false;
            prod.IsDeleted = true;
            await _db.SaveChangesAsync();

            await _auditService.LogAsync("ArchiveOrderedProduct", "Product", prod.Id.ToString(),
                new { prod.Name, HadOrderHistory = true },
                new { IsAvailable = false, IsDeleted = true });

            TempData["ToastWarning"] = $"Sản phẩm '{prod.Name}' đã phát sinh trong lịch sử đơn hàng nên được chuyển sang lưu trữ ẩn (Ngừng kinh doanh) để bảo toàn hóa đơn cũ.";
            return RedirectToAction(nameof(Products));
        }

        prod.IsAvailable = false;
        prod.IsDeleted = true;
        await _db.SaveChangesAsync();

        await _auditService.LogAsync("SoftDeleteProduct", "Product", prod.Id.ToString(),
            new { prod.Name }, new { IsDeleted = true });

        TempData["ToastSuccess"] = $"Đã xóa sản phẩm '{prod.Name}' khỏi danh mục.";
        return RedirectToAction(nameof(Products));
    }

    private async Task PopulateProductFormLookupsAsync(ProductFormViewModel vm)
    {
        vm.Categories = await _db.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        vm.AllToppings = await _db.Toppings
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync();

        vm.AllIngredients = await _db.Ingredients
            .AsNoTracking()
            .Where(i => !i.IsDeleted && i.IsActive)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }
}
