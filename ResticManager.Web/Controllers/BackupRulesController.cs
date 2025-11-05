using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Utils;
using ResticManager.Data;
using ResticManager.Models;

namespace ResticManager.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BackupRulesController : ControllerBase
{
    private readonly ResticManagerDbContext _context;
    private readonly JwtUtils _jwtUtils;

    public BackupRulesController(ResticManagerDbContext context, JwtUtils jwtUtils)
    {
        _context = context;
        _jwtUtils = jwtUtils;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var rule = await _context.BackupRules
            .FirstOrDefaultAsync(r => r.Id == id);

        if (rule == null)
            return NotFound();

        return Ok(rule);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // 获取所有规则
        var rules = await _context.BackupRules.ToListAsync();
        
        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BackupRule rule)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var createdRule = await _context.BackupRules.AddAsync(rule);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = createdRule.Entity.Id }, createdRule.Entity);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] BackupRule rule)
    {
        if (id != rule.Id)
            return BadRequest();

        _context.Entry(rule).State = EntityState.Modified;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await _context.BackupRules.FindAsync(id);

        if (rule == null)
            return NotFound();

        _context.BackupRules.Remove(rule);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}