using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication3.Models.DTOs;

namespace WebApplication3.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TripsController : ControllerBase
{
    private readonly string _connectionString;

    public TripsController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    
    /// pobrac wszystkie dostepne wycieczki wraz z informacja o krajach
   
    [HttpGet]
    public async Task<IActionResult> GetTrips()
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            
            // Zapytanie SQL: Pobranie wycieczek wraz z krajami
            // - Łączy tabele Trip, Country_Trip i Country
            // - Sortuje wyniki malejąco według daty rozpoczęcia (DateFrom)
            var query = @"
            SELECT 
                t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                c.Name AS CountryName
            FROM Trip t
            JOIN Country_Trip ct ON t.IdTrip = ct.IdTrip
            JOIN Country c ON ct.IdCountry = c.IdCountry
            ORDER BY t.DateFrom DESC";

            var trips = new Dictionary<int, TripDto>();
            using (var command = new SqlCommand(query, connection))
            using (var reader = await command.ExecuteReaderAsync())
            {
                // Przetwarzanie wyników zapytania:
                // - Grupowanie krajów dla tej samej wycieczki
                // - Unikanie duplikatów wycieczek
                while (await reader.ReadAsync())
                {
                    var tripId = reader.GetInt32(0);
                    if (!trips.ContainsKey(tripId))
                    {
                        trips[tripId] = new TripDto
                        {
                            IdTrip = tripId,
                            Name = reader.GetString(1),
                            Description = reader.GetString(2),
                            DateFrom = reader.GetDateTime(3),
                            DateTo = reader.GetDateTime(4),
                            MaxPeople = reader.GetInt32(5),
                            Countries = new List<string>()
                        };
                    }
                    // Dodawanie kraju do listy krajów wycieczki
                    trips[tripId].Countries.Add(reader.GetString(6));
                }
            }
            return Ok(trips.Values.ToList());
        }
    }
}