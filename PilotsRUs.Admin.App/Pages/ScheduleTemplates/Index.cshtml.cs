using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.ScheduleTemplates;

namespace PilotsRUs.Admin.App.Pages.ScheduleTemplates;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    // The "Api" HttpClient has no custom JSON options configured, so it doesn't inherit Program.cs's
    // (API.WebApi's) ConfigureHttpJsonOptions JsonStringEnumConverter registration - needed explicitly to
    // deserialize ScheduleFrequency's string form ("Daily") from the API response.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public List<ScheduleTemplateResponse> ScheduleTemplates { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/schedule-templates");
        if (response.IsSuccessStatusCode)
        {
            ScheduleTemplates = await response.Content.ReadFromJsonAsync<List<ScheduleTemplateResponse>>(JsonOptions) ?? [];
        }
    }
}
