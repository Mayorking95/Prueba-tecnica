using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba.Application.Dtos;
using Prueba.Application.Interfaces;
using Prueba.Domain.Enums;

namespace Prueba.Api.Controllers;

[ApiController]
[Route("api/loans")]
[Authorize]
public class LoansController(ILoanService loans) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<LoanDto>> Create(CreateLoanRequest request, CancellationToken ct)
    {
        var created = await loans.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<LoanDto>>> ListMine(CancellationToken ct) =>
        Ok(await loans.ListMineAsync(ct));

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<LoanDto>>> ListAll([FromQuery] LoanStatus? status, CancellationToken ct) =>
        Ok(await loans.ListAllAsync(status, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LoanDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await loans.GetByIdAsync(id, ct));

    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<LoanStatusDto>> GetStatus(Guid id, CancellationToken ct) =>
        Ok(await loans.GetStatusAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LoanDto>> Update(Guid id, UpdateLoanRequest request, CancellationToken ct) =>
        Ok(await loans.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await loans.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LoanDto>> Approve(Guid id, CancellationToken ct) =>
        Ok(await loans.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LoanDto>> Reject(Guid id, CancellationToken ct) =>
        Ok(await loans.RejectAsync(id, ct));
}
