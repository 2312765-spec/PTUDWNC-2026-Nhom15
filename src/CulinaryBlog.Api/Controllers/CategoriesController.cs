using CulinaryBlog.Application.Categories.Commands.CreateCategory;
using CulinaryBlog.Application.Categories.Commands.UpdateCategory;
using CulinaryBlog.Application.Categories.Commands.DeleteCategory;
using CulinaryBlog.Application.Categories.DTOs;
using CulinaryBlog.Application.Categories.Queries.GetCategories;
using CulinaryBlog.Application.Categories.Queries.GetCategoryBySlug;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CulinaryBlog.WebApi.Controllers;

[ApiController]
[Route("api/v1/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// FR-CAT-001: Xem Danh sách Tất cả Danh mục
    /// Kết quả được cache với IMemoryCache (TTL 60 phút sliding) và sắp xếp theo Name tăng dần.
    /// </summary>
    /// <response code="200">Trả về mảng CategoryDto[] (kể cả khi trống)</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCategoriesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// FR-CAT-002: Xem Chi tiết Danh mục và Danh sách Công thức phân trang
    /// Guest chỉ thấy Published; Author thấy thêm Draft của mình; Admin thấy tất cả.
    /// </summary>
    /// <param name="slug">SEO friendly slug của danh mục</param>
    /// <param name="page">Số trang (mặc định: 1)</param>
    /// <param name="pageSize">Số mục mỗi trang (mặc định: 12)</param>
    /// <response code="200">Thông tin danh mục kèm danh sách công thức phân trang</response>
    /// <response code="404">Không tìm thấy danh mục (RFC 7807 Problem Details)</response>
    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CategoryDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDetailDto>> GetBySlug(
        [FromRoute] string slug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetCategoryBySlugQuery(slug, page, pageSize), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// FR-CAT-003: Tạo Danh mục Mới [Admin]
    /// Yêu cầu xác thực với Role Admin. Tự động sinh Slug, chống trùng tên, và xóa cache "categories:all".
    /// </summary>
    /// <response code="201">Tạo thành công. Trả về CategoryDto kèm Location header</response>
    /// <response code="403">Không có quyền Admin (Forbidden)</response>
    /// <response code="409">Tên danh mục đã tồn tại (Conflict)</response>
    /// <response code="422">Dữ liệu không hợp lệ (Unprocessable Entity)</response>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        
        // Trả về HTTP 201 Created với CategoryDto và Location header trỏ đến /api/v1/categories/{newSlug}
        return CreatedAtAction(nameof(GetBySlug), new { slug = result.Slug }, result);
    }

    /// <summary>
    /// FR-CAT-004: Cập nhật Danh mục [Admin]
    /// Cập nhật Name, Description, ImageUrl, OrderIndex. Slug KHÔNG đổi để tránh gãy link SEO. Invalidate cache.
    /// </summary>
    /// <response code="200">Cập nhật thành công. Trả về CategoryDto đã cập nhật</response>
    /// <response code="403">Không có quyền Admin</response>
    /// <response code="404">Không tìm thấy danh mục</response>
    /// <response code="409">Tên danh mục trùng lặp với danh mục khác</response>
    /// <response code="422">Dữ liệu không hợp lệ</response>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CategoryDto>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest(new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Title = "Bad Request",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Route ID không khớp với Body ID."
            });
        }

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// FR-CAT-005: Xóa Danh mục [Admin]
    /// Ràng buộc: KHÔNG được xóa nếu danh mục còn chứa công thức (Published hoặc Draft). Invalidate cache.
    /// </summary>
    /// <response code="204">Xóa thành công (No Content)</response>
    /// <response code="403">Không có quyền Admin</response>
    /// <response code="404">Không tìm thấy danh mục</response>
    /// <response code="409">Danh mục còn chứa công thức (Conflict)</response>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteCategoryCommand(id), cancellationToken);
        return NoContent();
    }
}
