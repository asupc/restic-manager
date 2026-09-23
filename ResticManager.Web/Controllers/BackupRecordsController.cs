using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ResticManager.Application.Services.Abstractions;
using ResticManager.Data;
using ResticManager.Models;
using ResticManager.Models.Dtos;

namespace ResticManager.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BackupRecordsController : ControllerBase
    {
        private readonly IBackupRecordService _recordService;
        private readonly ResticManagerDbContext _context;

        public BackupRecordsController(IBackupRecordService recordService, ResticManagerDbContext context)
        {
            _recordService = recordService;
            _context = context;
        }

        /// <summary>
        /// 获取所有备份记录
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BackupRecord>>> GetAll()
        {
            var records = await _context.BackupRecords
                .Include(r => r.Task)
                .OrderByDescending(r => r.StartTime)
                .ToListAsync();
            
            return Ok(records);
        }

        /// <summary>
        /// 分页获取备份记录（支持任务ID、状态、关键词筛选）
        /// </summary>
        [HttpGet("paged")]
        public async Task<ActionResult<PagedResult<BackupRecord>>> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] int? taskId = null,
            [FromQuery] string? status = null,
            [FromQuery] string? keyword = null)
        {
            var pagedResult = await _recordService.GetPagedRecordsAsync(page, pageSize, taskId, status, keyword);
            return Ok(pagedResult);
        }

        /// <summary>
        /// 根据任务ID获取备份记录
        /// </summary>
        /// <param name="taskId">任务ID</param>
        [HttpGet("task/{taskId}")]
        public async Task<ActionResult<IEnumerable<BackupRecord>>> GetByTaskId(int taskId)
        {
            var records = await _recordService.GetRecordsByTaskIdAsync(taskId);
            return Ok(records);
        }

        /// <summary>
        /// 分页获取指定任务的备份记录
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="page">页码</param>
        /// <param name="pageSize">每页条数</param>
        [HttpGet("task/{taskId}/paged")]
        public async Task<ActionResult<PagedResult<BackupRecord>>> GetPagedByTaskId(
            int taskId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var pagedResult = await _recordService.GetPagedRecordsByTaskIdAsync(taskId, page, pageSize);
            return Ok(pagedResult);
        }

        /// <summary>
        /// 获取指定ID的备份记录
        /// </summary>
        /// <param name="id">备份记录ID</param>
        [HttpGet("{id}")]
        public async Task<ActionResult<BackupRecord>> GetById(int id)
        {
            var record = await _recordService.GetRecordByIdAsync(id);
            if (record == null)
            {
                return NotFound(new { message = "指定的备份记录不存在" });
            }

            return Ok(record);
        }

        /// <summary>
        /// 创建新的备份记录
        /// </summary>
        /// <param name="record">备份记录对象</param>
        [HttpPost]
        public async Task<ActionResult<BackupRecord>> Create([FromBody] BackupRecord record)
        {
            // 验证关联的任务是否存在
            var task = await _context.BackupTasks.FindAsync(record.BackupTaskId);
            if (task == null)
            {
                return BadRequest(new { message = "指定的备份任务不存在" });
            }

            var createdRecord = await _recordService.CreateRecordAsync(record);
            return CreatedAtAction(nameof(GetById), new { id = createdRecord.Id }, createdRecord);
        }

        /// <summary>
        /// 更新备份记录
        /// </summary>
        /// <param name="id">备份记录ID</param>
        /// <param name="record">更新的备份记录对象</param>
        [HttpPut("{id}")]
        public async Task<ActionResult<BackupRecord>> Update(int id, [FromBody] BackupRecord record)
        {
            if (id != record.Id)
            {
                return BadRequest(new { message = "记录ID不匹配" });
            }

            // 验证关联的任务是否存在
            var task = await _context.BackupTasks.FindAsync(record.BackupTaskId);
            if (task == null)
            {
                return BadRequest(new { message = "指定的备份任务不存在" });
            }

            var updatedRecord = await _recordService.UpdateRecordAsync(id, record);
            if (updatedRecord == null)
            {
                return NotFound(new { message = "指定的备份记录不存在" });
            }

            return Ok(updatedRecord);
        }

        /// <summary>
        /// 删除备份记录
        /// </summary>
        /// <param name="id">备份记录ID</param>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var result = await _recordService.DeleteRecordAsync(id);
            if (!result)
            {
                return NotFound(new { message = "指定的备份记录不存在" });
            }

            return NoContent();
        }
    }
}