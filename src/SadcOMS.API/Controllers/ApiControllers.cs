using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SadcOMS.API.DTOs;
using SadcOMS.API.Mapping;
using SadcOMS.API.Services;
using SadcOMS.Domain.Enums;
using SadcOMS.Domain.Services;

namespace SadcOMS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly CustomerService _service;

    public CustomersController(CustomerService service) => _service = service;

    // GET endpoints remain anonymous; add [Authorize] here to require authentication for reads.
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<CustomerResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _service.SearchAsync(search, page, pageSize, ct);
        return Ok(result);
    }
}

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly OrderService _orderService;
    private readonly IdempotencyService _idempotencyService;

    public OrdersController(OrderService orderService, IdempotencyService idempotencyService)
    {
        _orderService = orderService;
        _idempotencyService = idempotencyService;
    }

    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _orderService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _orderService.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<OrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? customerId,
        [FromQuery] OrderStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = null,
        CancellationToken ct = default)
    {
        var result = await _orderService.SearchAsync(customerId, status, page, pageSize, sort, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken ct)
    {
        const string idempotencyHeader = "Idempotency-Key";
        var requestPath = Request.Path.Value!;
        if (Request.Headers.TryGetValue(idempotencyHeader, out var idempotencyKey) &&
            !string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _idempotencyService.GetExistingAsync(idempotencyKey!, requestPath, ct);
            if (existing is not null)
            {
                Response.StatusCode = existing.StatusCode;
                return Content(existing.ResponseBody, "application/json");
            }
        }

        try
        {
            var result = await _orderService.UpdateStatusAsync(id, request, ct);
            var json = EntityMapper.SerializeOrderResponse(result);

            if (Request.Headers.TryGetValue(idempotencyHeader, out idempotencyKey) &&
                !string.IsNullOrWhiteSpace(idempotencyKey))
            {
                await _idempotencyService.StoreAsync(
                    idempotencyKey!, requestPath, json, StatusCodes.Status200OK, ct);
            }

            return Content(json, "application/json");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(ex.Message));
        }
        catch (InvalidOrderStatusTransitionException ex)
        {
            return Conflict(new ErrorResponse(ex.Message));
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(new ErrorResponse(ex.Message));
        }
        catch (FormatException)
        {
            return BadRequest(new ErrorResponse("Invalid RowVersion format. Expected Base64-encoded value."));
        }
    }
}

[ApiController]
[Route("api/reports/orders")]
public class ReportsController : ControllerBase
{
    private readonly ReportService _service;

    public ReportsController(ReportService service) => _service = service;

    [HttpGet("zar")]
    [ProducesResponseType(typeof(OrderZarReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrdersInZar(CancellationToken ct)
    {
        var result = await _service.GetOrdersInZarAsync(ct);
        return Ok(result);
    }
}
