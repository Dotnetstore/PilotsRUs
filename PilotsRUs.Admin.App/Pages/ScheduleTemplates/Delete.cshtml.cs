using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.ScheduleTemplates;

namespace PilotsRUs.Admin.App.Pages.ScheduleTemplates;

public sealed class DeleteModel(IHttpClientFactory httpClientFactory) : PageModel
{
    // See IndexModel.JsonOptions - the "Api" HttpClient doesn't inherit the API's server-side
    // JsonStringEnumConverter registration, so ScheduleFrequency needs it passed explicitly.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public ScheduleTemplateResponse? TargetScheduleTemplate { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/schedule-templates/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        TargetScheduleTemplate = await response.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.DeleteAsync($"/schedule-templates/{Id}");

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/ScheduleTemplates/Index");
        }

        ErrorMessage = "Delete failed.";

        var getResponse = await client.GetAsync($"/schedule-templates/{Id}");
        if (getResponse.IsSuccessStatusCode)
        {
            TargetScheduleTemplate = await getResponse.Content.ReadFromJsonAsync<ScheduleTemplateResponse>(JsonOptions);
        }

        return Page();
    }
}
