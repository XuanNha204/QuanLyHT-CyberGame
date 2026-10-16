using CyberGame.Application.Common;
using CyberGame.Application.DTOs;
using CyberGame.Domain.Entities;
using CyberGame.Domain.Enums;
using CyberGame.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace CyberGame.Application.Services;

public interface ICatalogInventoryService
{
    Task<IReadOnlyList<ProductCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<ProductCategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default);
    Task<ProductCategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);

    Task<PagedResult<ProductDto>> GetProductsAsync(Guid? categoryId = null, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ProductDto> GetProductByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default);
    Task<ProductDto> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(bool lowStockOnly = false, CancellationToken ct = default);
    Task<InventoryItemDto> ReceiveInventoryAsync(ReceiveInventoryRequest request, CancellationToken ct = default);
    Task<InventoryItemDto> AdjustInventoryAsync(AdjustInventoryRequest request, CancellationToken ct = default);
    Task<PagedResult<InventoryTransactionDto>> GetInventoryTransactionsAsync(Guid? productId = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
}

public class CatalogInventoryService : ICatalogInventoryService
{
    private readonly ICyberGameDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CatalogInventoryService(ICyberGameDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    private async Task<Guid> GetBranchIdAsync(CancellationToken ct)
    {
        if (_currentUser.BranchId.HasValue) return _currentUser.BranchId.Value;
        var b = await _db.Branches.FirstOrDefaultAsync(x => x.IsActive, ct);
        return b?.Id ?? throw new NotFoundException("Không tìm thấy chi nhánh.", "branch_not_found");
    }

    public async Task<IReadOnlyList<ProductCategoryDto>> GetCategoriesAsync(CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var categories = await _db.ProductCategories
            .AsNoTracking()
            .Where(c => c.BranchId == branchId && c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ProductCategoryDto(
                c.Id,
                c.BranchId,
                c.Name,
                c.SortOrder,
                c.IsActive,
                c.Products.Count(p => p.IsActive)
            ))
            .ToListAsync(ct);

        return categories;
    }

    public async Task<ProductCategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var name = request.Name.Trim();

        if (await _db.ProductCategories.AnyAsync(c => c.BranchId == branchId && c.Name == name, ct))
        {
            throw new ConflictException("Tên danh mục đã tồn tại.", "category_exists");
        }

        var category = new ProductCategory
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Name = name,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync(ct);

        return new ProductCategoryDto(category.Id, category.BranchId, category.Name, category.SortOrder, category.IsActive, 0);
    }

    public async Task<ProductCategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var category = await _db.ProductCategories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id && c.BranchId == branchId, ct);
        if (category == null) throw new NotFoundException("Không tìm thấy danh mục.", "category_not_found");

        category.Name = request.Name.Trim();
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;

        await _db.SaveChangesAsync(ct);
        return new ProductCategoryDto(category.Id, category.BranchId, category.Name, category.SortOrder, category.IsActive, category.Products.Count(p => p.IsActive));
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(Guid? categoryId = null, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Balance)
            .Where(p => p.BranchId == branchId);

        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(s) || p.Sku.ToLower().Contains(s));
        }

        int total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<ProductDto>(dtos, total, page, pageSize);
    }

    public async Task<ProductDto> GetProductByIdAsync(Guid id, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Balance)
            .FirstOrDefaultAsync(p => p.Id == id && p.BranchId == branchId, ct);

        if (product == null) throw new NotFoundException("Không tìm thấy sản phẩm.", "product_not_found");
        return MapToDto(product);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var sku = request.Sku.Trim().ToUpperInvariant();

        if (await _db.Products.AnyAsync(p => p.BranchId == branchId && p.Sku == sku, ct))
        {
            throw new ConflictException("Mã SKU sản phẩm đã tồn tại trong chi nhánh.", "sku_exists");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            CategoryId = request.CategoryId,
            Sku = sku,
            Name = request.Name.Trim(),
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "cái" : request.Unit.Trim(),
            SalePrice = request.SalePrice,
            CostPrice = request.CostPrice,
            TrackInventory = request.TrackInventory,
            ReorderLevel = request.ReorderLevel,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var balance = new InventoryBalance
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            ProductId = product.Id,
            QuantityOnHand = request.InitialStock,
            UpdatedAtUtc = DateTime.UtcNow
        };

        product.Balance = balance;
        _db.Products.Add(product);
        _db.InventoryBalances.Add(balance);

        if (request.InitialStock > 0)
        {
            var tx = new InventoryTransaction
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                ProductId = product.Id,
                Type = InventoryTransactionType.Opening,
                Quantity = request.InitialStock,
                QuantityBefore = 0,
                QuantityAfter = request.InitialStock,
                UnitCost = request.CostPrice,
                Note = "Tồn kho ban đầu",
                OccurredAtUtc = DateTime.UtcNow,
                CreatedByUserId = _currentUser.UserId ?? Guid.Empty
            };
            _db.InventoryTransactions.Add(tx);
        }

        await _db.SaveChangesAsync(ct);
        return await GetProductByIdAsync(product.Id, ct);
    }

    public async Task<ProductDto> UpdateProductAsync(Guid id, UpdateProductRequest request, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Balance)
            .FirstOrDefaultAsync(p => p.Id == id && p.BranchId == branchId, ct);

        if (product == null) throw new NotFoundException("Không tìm thấy sản phẩm.", "product_not_found");

        product.CategoryId = request.CategoryId;
        product.Name = request.Name.Trim();
        product.Unit = request.Unit.Trim();
        product.SalePrice = request.SalePrice;
        product.CostPrice = request.CostPrice;
        product.TrackInventory = request.TrackInventory;
        product.ReorderLevel = request.ReorderLevel;
        product.IsActive = request.IsActive;
        product.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToDto(product);
    }

    public async Task<IReadOnlyList<InventoryItemDto>> GetInventoryAsync(bool lowStockOnly = false, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Balance)
            .Where(p => p.BranchId == branchId && p.IsActive && p.TrackInventory);

        if (lowStockOnly)
        {
            query = query.Where(p => p.Balance != null && p.Balance.QuantityOnHand <= p.ReorderLevel);
        }

        var items = await query.OrderBy(p => p.Name).ToListAsync(ct);

        return items.Select(p =>
        {
            decimal qty = p.Balance?.QuantityOnHand ?? 0m;
            return new InventoryItemDto(
                p.Id,
                p.Sku,
                p.Name,
                p.Unit,
                p.Category.Name,
                qty,
                p.ReorderLevel,
                qty <= p.ReorderLevel,
                p.CostPrice,
                p.SalePrice
            );
        }).ToList();
    }

    public async Task<InventoryItemDto> ReceiveInventoryAsync(ReceiveInventoryRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0) throw new BusinessRuleException("Số lượng nhập kho phải lớn hơn 0.", "invalid_quantity");
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Balance)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.BranchId == branchId, ct);

        if (product == null) throw new NotFoundException("Không tìm thấy sản phẩm.", "product_not_found");

        if (product.Balance == null)
        {
            product.Balance = new InventoryBalance
            {
                Id = Guid.NewGuid(),
                BranchId = product.BranchId,
                ProductId = product.Id,
                QuantityOnHand = 0,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.InventoryBalances.Add(product.Balance);
        }

        decimal before = product.Balance.QuantityOnHand;
        decimal after = before + request.Quantity;

        product.Balance.QuantityOnHand = after;
        product.Balance.UpdatedAtUtc = DateTime.UtcNow;
        product.CostPrice = request.UnitCost;

        var tx = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BranchId = product.BranchId,
            ProductId = product.Id,
            Type = InventoryTransactionType.Receipt,
            Quantity = request.Quantity,
            QuantityBefore = before,
            QuantityAfter = after,
            UnitCost = request.UnitCost,
            Note = request.Note?.Trim() ?? "Nhập hàng vào kho",
            OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        _db.InventoryTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        return new InventoryItemDto(
            product.Id,
            product.Sku,
            product.Name,
            product.Unit,
            product.Category.Name,
            after,
            product.ReorderLevel,
            after <= product.ReorderLevel,
            product.CostPrice,
            product.SalePrice
        );
    }

    public async Task<InventoryItemDto> AdjustInventoryAsync(AdjustInventoryRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new BusinessRuleException("Vui lòng nhập lý do điều chỉnh tồn kho.", "reason_required");
        var currentUserId = _currentUser.UserId ?? throw new DomainException("Chưa đăng nhập.", "unauthenticated");
        var branchId = await GetBranchIdAsync(ct);

        var product = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Balance)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.BranchId == branchId, ct);

        if (product == null) throw new NotFoundException("Không tìm thấy sản phẩm.", "product_not_found");

        if (product.Balance == null)
        {
            product.Balance = new InventoryBalance
            {
                Id = Guid.NewGuid(),
                BranchId = product.BranchId,
                ProductId = product.Id,
                QuantityOnHand = 0,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _db.InventoryBalances.Add(product.Balance);
        }

        decimal before = product.Balance.QuantityOnHand;
        decimal after = request.NewQuantity;
        decimal delta = after - before;

        if (delta == 0)
        {
            throw new BusinessRuleException("Số lượng mới không thay đổi so với tồn hiện tại.", "no_change");
        }

        product.Balance.QuantityOnHand = after;
        product.Balance.UpdatedAtUtc = DateTime.UtcNow;

        var tx = new InventoryTransaction
        {
            Id = Guid.NewGuid(),
            BranchId = product.BranchId,
            ProductId = product.Id,
            Type = InventoryTransactionType.Adjustment,
            Quantity = delta,
            QuantityBefore = before,
            QuantityAfter = after,
            UnitCost = product.CostPrice,
            Note = $"Điều chỉnh kiểm kê: {request.Reason.Trim()}",
            OccurredAtUtc = DateTime.UtcNow,
            CreatedByUserId = currentUserId
        };

        _db.InventoryTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        return new InventoryItemDto(
            product.Id,
            product.Sku,
            product.Name,
            product.Unit,
            product.Category.Name,
            after,
            product.ReorderLevel,
            after <= product.ReorderLevel,
            product.CostPrice,
            product.SalePrice
        );
    }

    public async Task<PagedResult<InventoryTransactionDto>> GetInventoryTransactionsAsync(Guid? productId = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var branchId = await GetBranchIdAsync(ct);
        var query = _db.InventoryTransactions
            .AsNoTracking()
            .Include(t => t.Product)
            .Include(t => t.CreatedByUser)
            .Where(t => t.BranchId == branchId)
            .AsQueryable();

        if (productId.HasValue) query = query.Where(t => t.ProductId == productId.Value);

        int total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(t => t.OccurredAtUtc)
                               .Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync(ct);

        var dtos = items.Select(t => new InventoryTransactionDto(
            t.Id,
            t.ProductId,
            t.Product.Name,
            t.Product.Sku,
            t.Type,
            t.Quantity,
            t.QuantityBefore,
            t.QuantityAfter,
            t.UnitCost,
            t.Note,
            t.OccurredAtUtc,
            t.CreatedByUser?.FullName ?? "Hệ thống"
        )).ToList();

        return new PagedResult<InventoryTransactionDto>(dtos, total, page, pageSize);
    }

    private static ProductDto MapToDto(Product p)
    {
        decimal qty = p.Balance?.QuantityOnHand ?? 0m;
        return new ProductDto(
            p.Id,
            p.BranchId,
            p.CategoryId,
            p.Category?.Name ?? string.Empty,
            p.Sku,
            p.Name,
            p.Unit,
            p.SalePrice,
            p.CostPrice,
            p.TrackInventory,
            p.ReorderLevel,
            qty,
            p.IsActive,
            p.TrackInventory && qty <= p.ReorderLevel
        );
    }
}
