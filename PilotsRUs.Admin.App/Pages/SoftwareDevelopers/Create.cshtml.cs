using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.Admin.App.Pages.SoftwareDevelopers;

public sealed class CreateModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PostAsJsonAsync("/software-developers", new CreateSoftwareDeveloperRequest(Input.Name));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/SoftwareDevelopers/Index");
        }

        // SoftwareDeveloper has no structured error shape - the only validation rule (duplicate Name)
        // returns Results.Conflict(string), same pattern Manufacturers already uses.
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Create failed.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Create failed.");
        }

        return Page();
    }

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;
    }
}
