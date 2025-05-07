using System.ComponentModel.DataAnnotations;

namespace WebApplication3.Models;



public class ClientRequest
{
    [Required]
    public string FirstName { get; set; }
    [Required]
    public string LastName { get; set; }
    [EmailAddress]
    public string Email { get; set; }
    public string Telephone { get; set; }
    [StringLength(11, MinimumLength = 11)]
    public string Pesel { get; set; }
}