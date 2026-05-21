using FluentAssertions;
using Learnexia.Modules.Catalog.Application.Features.Products.Dtos;
using Xunit;

namespace Modules.Catalog.UnitTests;

public sealed class ProductDtoTests
{
    [Fact]
    public void AddProductDto_carries_category_and_price()
    {
        var dto = new AddProductDto { Name = "Course", Price = 10m, CategoryId = 3, Description = "x" };
        dto.CategoryId.Should().Be(3);
        dto.Price.Should().Be(10m);
    }
}
