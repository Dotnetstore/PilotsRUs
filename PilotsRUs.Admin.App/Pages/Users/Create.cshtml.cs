using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PilotsRUs.Shared.SDK.Users;

namespace PilotsRUs.Admin.App.Pages.Users;

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
        var response = await client.PostAsJsonAsync(
            "/users",
            new CreateUserRequest(Input.Email, Input.FirstName, Input.LastName, Input.Password, Input.IsAdmin));

        if (response.IsSuccessStatusCode)
        {
            return RedirectToPage("/Users/Index");
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

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }
    }
}
