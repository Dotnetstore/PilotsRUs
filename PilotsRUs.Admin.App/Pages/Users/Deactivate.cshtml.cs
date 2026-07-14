using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Users;

namespace PilotsRUs.Admin.App.Pages.Users;

public sealed class DeactivateModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public UserResponse? TargetUser { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/users/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        TargetUser = await response.Content.ReadFromJsonAsync<UserResponse>();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PostAsync($"/users/{Id}/deactivate", null);

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Users/Index");
        }

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest)
        {
            // Both guards on this endpoint (self-deactivation, last-active-admin) return a plain-text
            // message via Results.BadRequest(string)/Results.Conflict(string), which serializes as a JSON
            // string literal.
            ErrorMessage = await response.Content.ReadFromJsonAsync<string>() ?? "Deactivation failed.";
        }
        else
        {
            ErrorMessage = "Deactivation failed.";
        }

        var getResponse = await client.GetAsync($"/users/{Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            TargetUser = await getResponse.Content.ReadFromJsonAsync<UserResponse>();
        }

        return Page();
    }
}
