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
    private readonly GitHubCosmosDbStore _dataStore;

    public UmbracoCommunityGitHubUsersApiController(
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        GitHubCosmosDbStore dataStore)
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

    [HttpGet("hqmembers/{login}")]
    [ProducesResponseType<GitHubHqMember>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetHqMember(string login)
    {
        var member = _dataStore.GetHqMembers().FirstOrDefault(m => m.Login == login);
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

        member.Id = member.Login; // Use Login as ID
        _dataStore.UpsertHqMembers(new[] { member });

        // Retrieve the inserted member
        var created = _dataStore.GetHqMembers().First(m => m.Login == member.Login);
        return CreatedAtAction(nameof(GetHqMember), new { login = created.Login }, created);
    }

    [HttpPut("hqmembers/{login}")]
    [ProducesResponseType<GitHubHqMember>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult UpdateHqMember(string login, [FromBody] GitHubHqMember member)
    {
        var existing = _dataStore.GetHqMembers().FirstOrDefault(m => m.Login == login);
        if (existing == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(member.Login))
        {
            return BadRequest("Login is required");
        }

        // Check if login is being changed to one that already exists
        if (member.Login != login)
        {
            return BadRequest("Cannot change login");
        }

        member.Id = member.Login; // Ensure the ID matches
        _dataStore.UpsertHqMembers(new[] { member });

        var updated = _dataStore.GetHqMembers().First(m => m.Login == login);
        return Ok(updated);
    }

    [HttpDelete("hqmembers/{login}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DeleteHqMember(string login)
    {
        var existing = _dataStore.GetHqMembers().FirstOrDefault(m => m.Login == login);
        if (existing == null)
        {
            return NotFound();
        }

        _dataStore.DeleteHqMember(login);
        return NoContent();
    }
}