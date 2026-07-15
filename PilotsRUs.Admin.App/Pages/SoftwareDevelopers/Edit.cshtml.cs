using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.SoftwareDevelopers;

namespace PilotsRUs.Admin.App.Pages.SoftwareDevelopers;

public sealed class EditModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/software-developers/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var softwareDeveloper = await response.Content.ReadFromJsonAsync<SoftwareDeveloperResponse>();
        if (softwareDeveloper is null)
        {
            return NotFound();
        }

        Input = new InputModel { Name = softwareDeveloper.Name };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PutAsJsonAsync($"/software-developers/{Id}", new UpdateSoftwareDeveloperRequest(Input.Name));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/SoftwareDevelopers/Index");
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Update failed.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Update failed.");
        }

        return Page();
    }

    public sealed class InputModel
    {
        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;
    }
}
