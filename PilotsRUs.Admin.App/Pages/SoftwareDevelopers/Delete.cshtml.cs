using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.Admin.App.Pages.SoftwareDevelopers;

public sealed class DeleteModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public SoftwareDeveloperResponse? TargetSoftwareDeveloper { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/software-developers/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        TargetSoftwareDeveloper = await response.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.DeleteAsync($"/software-developers/{Id}");

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/SoftwareDevelopers/Index");
        }

        // DeleteSoftwareDeveloper can return Results.Conflict(string) when aircraft still reference it -
        // surface that specific message instead of a generic one, same pattern Manufacturers/Delete uses.
        ErrorMessage = response.StatusCode == HttpStatusCode.Conflict
            ? await response.Content.ReadFromJsonAsync<string>() ?? "Delete failed."
            : "Delete failed.";

        var getResponse = await client.GetAsync($"/software-developers/{Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            TargetSoftwareDeveloper = await getResponse.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();
        }

        return Page();
    }
}
