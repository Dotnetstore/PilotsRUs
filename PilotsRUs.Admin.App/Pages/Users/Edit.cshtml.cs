using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Users;

namespace PilotsRUs.Admin.App.Pages.Users;

public sealed class EditModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = httpClientFactory.CreateClient("Api");
        var response = await client.GetAsync($"/users/{Id}");
        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        if (user is null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsAdmin = user.IsAdmin
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = httpClientFactory.CreateClient("Api");
        var response = await client.PutAsJsonAsync(
            $"/users/{Id}",
            new UpdateUserRequest(Input.Email, Input.FirstName, Input.LastName, Input.IsAdmin));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Users/Index");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            // The last-active-admin guard returns Results.Conflict(string), which serializes the message
            // as a JSON string literal, not raw text - unlike the UserValidationProblem shape below.
            var message = await response.Content.ReadFromJsonAsync<string>();
            ModelState.AddModelError(string.Empty, message ?? "Update failed.");
            return Page();
        }

        var problem = await response.Content.ReadFromJsonAsync<UserValidationProblem>();
        foreach (var error in problem?.Errors ?? [])
        {
            ModelState.AddModelError(string.IsNullOrEmpty(error.Field) ? string.Empty : $"Input.{error.Field}", error.Description);
        }

        return Page();
    }

    public sealed class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }
    }
}
