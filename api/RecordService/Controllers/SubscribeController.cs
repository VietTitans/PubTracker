using System.Net;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic.DigestService;
using RecordService.BusinessLogic.SearchQueriesService;
using RecordService.BusinessLogic.UsersService;

namespace RecordService.Controllers;

/// <summary>
/// Unauthenticated, unversioned (links in sent emails must keep working): the signed token is
/// the credential. GET only shows a confirmation form, because mail scanners prefetch links;
/// the state change happens on POST, which is also what a mail client's one-click
/// List-Unsubscribe-Post request sends to the same URL.
/// </summary>
[AllowAnonymous]
[ApiVersion("1.0")]
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class SubscribeController : ControllerBase
{
    private readonly UnsubscribeTokens _tokens;
    private readonly IUsersService _usersService;
    private readonly ISearchQueriesService _searchQueriesService;
    private readonly ILogger<SubscribeController> _logger;

    public SubscribeController(UnsubscribeTokens tokens, IUsersService usersService, ISearchQueriesService searchQueriesService, ILogger<SubscribeController> logger)
    {
        _tokens = tokens;
        _usersService = usersService;
        _searchQueriesService = searchQueriesService;
        _logger = logger;
    }

    [HttpGet("unsubscribe/{token}")]
    public IActionResult Confirm(string token)
    {
        if (!_tokens.TryValidate(token, out _, out var searchQueryId))
        {
            return Page(StatusCodes.Status400BadRequest, "This link is not valid.");
        }

        var what = searchQueryId == null ? "all Digests" : "Digests for this search";
        return Page(StatusCodes.Status200OK,
            $"<p>Stop {what}?</p><form method=\"post\"><button type=\"submit\">Unsubscribe</button></form>");
    }

    [HttpPost("unsubscribe/{token}")]
    public async Task<IActionResult> Apply(string token)
    {
        if (!_tokens.TryValidate(token, out var userId, out var searchQueryId))
        {
            return Page(StatusCodes.Status400BadRequest, "This link is not valid.");
        }

        try
        {
            if (searchQueryId is { } queryId)
            {
                // Already unsubscribed is fine: the outcome the user asked for holds.
                await _searchQueriesService.UnsubscribeAsync(userId, queryId);
            }
            else
            {
                await _usersService.SetDigestsStoppedAsync(userId, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying unsubscribe for user {UserId}", userId);
            return Page(StatusCodes.Status500InternalServerError, "Something went wrong. Please try again.");
        }

        return Page(StatusCodes.Status200OK, searchQueryId == null
            ? "You will no longer receive Digests. You can turn them back on in the app."
            : "You will no longer receive Digests for this search.");
    }

    private ContentResult Page(int statusCode, string bodyHtml) => new()
    {
        StatusCode = statusCode,
        ContentType = "text/html; charset=utf-8",
        Content = $"<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>PubTracker</title></head><body style=\"font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:4rem auto;padding:0 1rem;\">{(bodyHtml.StartsWith('<') ? bodyHtml : WebUtility.HtmlEncode(bodyHtml))}</body></html>"
    };
}
