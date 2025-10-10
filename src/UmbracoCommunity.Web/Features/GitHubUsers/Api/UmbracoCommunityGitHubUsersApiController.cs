using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;
using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Web.Features.GitHubUsers.Api;

[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "UmbracoCommunity.GitHubUsers")]
public class UmbracoCommunityGitHubUsersApiController : UmbracoCommunityGitHubUsersApiControllerBase
{
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly GitHubDataStore _dataStore;

    public UmbracoCommunityGitHubUsersApiController(
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        GitHubDataStore dataStore)
    {
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _dataStore = dataStore;
    }

    [HttpGet("hqmembers")]
    [ProducesResponseType<IEnumerable<GitHubHqMember>>(StatusCodes.Status200OK)]
    public IActionResult GetHqMembers()
    {
        var members = _dataStore.GetHqMembers();
        return Ok(members);
    }

    [HttpGet("hqmembers/{id}")]
    [ProducesResponseType<GitHubHqMember>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetHqMember(int id)
    {
        var member = _dataStore.GetHqMembers().FirstOrDefault(m => m.Id == id);
        if (member == null)
        {
            return NotFound();
        }
        return Ok(member);
    }

    [HttpPost("hqmembers")]
    [ProducesResponseType<GitHubHqMember>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CreateHqMember([FromBody] GitHubHqMember member)
    {
        if (string.IsNullOrWhiteSpace(member.Login))
        {
            return BadRequest("Login is required");
        }

        var existing = _dataStore.GetHqMembers().FirstOrDefault(m => m.Login == member.Login);
        if (existing != null)
        {
            return BadRequest("A member with this login already exists");
        }

        member.Id = 0; // Let the database assign the ID
        _dataStore.UpsertHqMembers(new[] { member });

        // Retrieve the inserted member to get the assigned ID
        var created = _dataStore.GetHqMembers().First(m => m.Login == member.Login);
        return CreatedAtAction(nameof(GetHqMember), new { id = created.Id }, created);
    }

    [HttpPut("hqmembers/{id}")]
    [ProducesResponseType<GitHubHqMember>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult UpdateHqMember(int id, [FromBody] GitHubHqMember member)
    {
        var existing = _dataStore.GetHqMembers().FirstOrDefault(m => m.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(member.Login))
        {
            return BadRequest("Login is required");
        }

        // Check if login is being changed to one that already exists
        var duplicateLogin = _dataStore.GetHqMembers()
            .FirstOrDefault(m => m.Login == member.Login && m.Id != id);
        if (duplicateLogin != null)
        {
            return BadRequest("A member with this login already exists");
        }

        member.Id = id; // Ensure the ID matches
        _dataStore.UpsertHqMembers(new[] { member });

        var updated = _dataStore.GetHqMembers().First(m => m.Id == id);
        return Ok(updated);
    }

    [HttpDelete("hqmembers/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteHqMember(int id)
    {
        var existing = _dataStore.GetHqMembers().FirstOrDefault(m => m.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        _dataStore.DeleteHqMember(id);
        return NoContent();
    }
}