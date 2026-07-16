using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Schedules;

namespace PilotsRUs.Admin.App.Pages.Schedules;

public sealed class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public List<ScheduleResponse> Schedules { get; set; } = [];

    public async Task OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync("/schedules");
        if (response.IsSuccessStatusCode)
        {
            Schedules = await response.Content.ReadFromJsonAsync<List<ScheduleResponse>>() ?? [];
        }
    }
}
