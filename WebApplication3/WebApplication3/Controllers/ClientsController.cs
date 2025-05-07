using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using WebApplication3.Models;
using WebApplication3.Models.DTOs;

namespace WebApplication3.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly string _connectionString;

    public ClientsController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection");
    }

    /// <summary>
    /// Stwozyc nowego klienta
    /// </summary>
    [HttpPost]
public async Task<IActionResult> CreateClient([FromBody] ClientRequest request)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    // Sprawdzenie, czy klient o podanym PESEL już istnieje
    var checkPeselQuery = "SELECT COUNT(*) FROM Client WHERE Pesel = @Pesel";
    using (var connection = new SqlConnection(_connectionString))
    using (var command = new SqlCommand(checkPeselQuery, connection))
    {
        command.Parameters.AddWithValue("@Pesel", request.Pesel);
        await connection.OpenAsync();
        var exists = (int)await command.ExecuteScalarAsync();
        if (exists > 0)
            return BadRequest("Client with this PESEL already exists ");
    }

    //Dodanie nowego klienta i zwrócenie wszystkich jego danych
    var insertQuery = @"
        INSERT INTO Client (FirstName, LastName, Email, Telephone, Pesel)
        OUTPUT INSERTED.*
        VALUES (@FirstName, @LastName, @Email, @Telephone, @Pesel)";

    using (var connection = new SqlConnection(_connectionString))
    using (var command = new SqlCommand(insertQuery, connection))
    {
        command.Parameters.AddWithValue("@FirstName", request.FirstName);
        command.Parameters.AddWithValue("@LastName", request.LastName);
        command.Parameters.AddWithValue("@Email", string.IsNullOrEmpty(request.Email) ? DBNull.Value : request.Email);
        command.Parameters.AddWithValue("@Telephone", string.IsNullOrEmpty(request.Telephone) ? DBNull.Value : request.Telephone);
        command.Parameters.AddWithValue("@Pesel", request.Pesel);

        await connection.OpenAsync();
        
        
        using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                var client = new ClientResponse
                {
                    IdClient = reader.GetInt32(reader.GetOrdinal("IdClient")),
                    FirstName = reader.GetString(reader.GetOrdinal("FirstName")),
                    LastName = reader.GetString(reader.GetOrdinal("LastName")),
                    Email = reader.IsDBNull("Email") ? null : reader.GetString("Email"),
                    Telephone = reader.IsDBNull("Telephone") ? null : reader.GetString("Telephone"),
                    Pesel = reader.GetString("Pesel")
                };
                return Ok(client); 
            }
        }
        return StatusCode(500, "Client was not created");
    }
}

    /// <summary>
    /// Pobranie wycieczek klienta
    /// </summary>
    [HttpGet("{idClient}/trips")]
    public async Task<IActionResult> GetClientTrips(int idClient)
    {
        try
        {
            
            var clientExists = await CheckEntityExists("Client", "IdClient", idClient);
            if (!clientExists)
                return NotFound("Client was not found");

            // Pobranie wycieczek klienta wraz ze szczegółami
            var query = @"
                SELECT 
                    t.IdTrip, t.Name, t.Description, t.DateFrom, t.DateTo, t.MaxPeople,
                    ct.RegisteredAt, ct.PaymentDate
                FROM Client_Trip ct
                JOIN Trip t ON ct.IdTrip = t.IdTrip
                WHERE ct.IdClient = @IdClient";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@IdClient", idClient);
                await connection.OpenAsync();

                var trips = new List<ClientTripResponse>();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        trips.Add(new ClientTripResponse
                        {
                            IdTrip = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Description = reader.GetString(2),
                            DateFrom = reader.GetDateTime(3),
                            DateTo = reader.GetDateTime(4),
                            MaxPeople = reader.GetInt32(5),
                            RegisteredAt = reader.GetInt32(6), 
                            PaymentDate = reader.IsDBNull(7) ? null : reader.GetInt32(7)
                        });
                    }
                }

                return Ok(trips);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Zarejestrowac klienta na wycieczke
    /// </summary>
    [HttpPut("{idClient}/trips/{idTrip}")]
    public async Task<IActionResult> RegisterForTrip(int idClient, int idTrip)
    {
        try
        {
            
            var clientExists = await CheckEntityExists("Client", "IdClient", idClient);
            if (!clientExists)
                return NotFound("Client was not found");

            var tripExists = await CheckEntityExists("Trip", "IdTrip", idTrip);
            if (!tripExists)
                return NotFound("Trip not found");

            //Sprawdzenie maksymalnej liczby uczestników wycieczki
            var maxPeopleQuery = "SELECT MaxPeople FROM Trip WHERE IdTrip = @IdTrip";
            int maxPeople;
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(maxPeopleQuery, connection))
            {
                command.Parameters.AddWithValue("@IdTrip", idTrip);
                await connection.OpenAsync();
                maxPeople = (int)await command.ExecuteScalarAsync();
            }
            
            //Sprawdzenie aktualnej liczby uczestników wycieczki
            var currentParticipantsQuery = "SELECT COUNT(*) FROM Client_Trip WHERE IdTrip = @IdTrip";
            int currentParticipants;
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(currentParticipantsQuery, connection))
            {
                command.Parameters.AddWithValue("@IdTrip", idTrip);
                await connection.OpenAsync();
                currentParticipants = (int)await command.ExecuteScalarAsync();
            }

            if (currentParticipants >= maxPeople)
                return BadRequest("Max People reached");

            
            var registeredAtDate = DateTime.Now.Date;
            var registeredAt = int.Parse(registeredAtDate.ToString("yyyyMMdd"));

            //Dodanie rejestracji klienta na wycieczkę
            var insertRegistrationQuery = @"
                INSERT INTO Client_Trip (IdClient, IdTrip, RegisteredAt, PaymentDate)
                VALUES (@IdClient, @IdTrip, @RegisteredAt, NULL)";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(insertRegistrationQuery, connection))
            {
                command.Parameters.AddWithValue("@IdClient", idClient);
                command.Parameters.AddWithValue("@IdTrip", idTrip);
                command.Parameters.AddWithValue("@RegisteredAt", registeredAt);

                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
                return Ok("Registration was succesfully inserted");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sprawdzanie istniecia encji w bazie
    /// </summary>
    private async Task<bool> CheckEntityExists(string tableName, string idColumn, int id)
    {
        //Sprawdzenie, czy rekord istnieje w tabeli
        var query = $"SELECT COUNT(*) FROM {tableName} WHERE {idColumn} = @Id";
        using (var connection = new SqlConnection(_connectionString))
        using (var command = new SqlCommand(query, connection))
        {
            command.Parameters.AddWithValue("@Id", id);
            await connection.OpenAsync();
            return (int)await command.ExecuteScalarAsync() > 0;
        }
    }
    
    /// <summary>
    /// usuniecie rejestracji klienta
    /// </summary>
   
   
    [HttpDelete("{idClient}/trips/{idTrip}")]
public async Task<IActionResult> DeleteClientTrip(int idClient, int idTrip)
{
    try
    {
        
        var clientExists = await CheckEntityExists("Client", "IdClient", idClient);
        if (!clientExists)
            return NotFound("Client was not found");

        var tripExists = await CheckEntityExists("Trip", "IdTrip", idTrip);
        if (!tripExists)
            return NotFound("Trip was not found");

        // Sprawdzenie, czy rejestracja istnieje
        var registrationExistsQuery = @"
            SELECT COUNT(*) 
            FROM Client_Trip 
            WHERE IdClient = @IdClient AND IdTrip = @IdTrip";

        using (var connection = new SqlConnection(_connectionString))
        using (var command = new SqlCommand(registrationExistsQuery, connection))
        {
            command.Parameters.AddWithValue("@IdClient", idClient);
            command.Parameters.AddWithValue("@IdTrip", idTrip);
            await connection.OpenAsync();
            var exists = (int)await command.ExecuteScalarAsync();
            if (exists == 0)
                return NotFound("Registration was not found");
        }

        // Usunięcie rejestracji klienta z wycieczki
        var deleteQuery = @"
            DELETE FROM Client_Trip 
            WHERE IdClient = @IdClient AND IdTrip = @IdTrip";

        using (var connection = new SqlConnection(_connectionString))
        using (var command = new SqlCommand(deleteQuery, connection))
        {
            command.Parameters.AddWithValue("@IdClient", idClient);
            command.Parameters.AddWithValue("@IdTrip", idTrip);
            await connection.OpenAsync();
            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
                return StatusCode(500, "Registration was not deleted");

            return Ok("Registration was deleted succesfully.");
        }
    }
    catch (Exception ex)
    {
        return StatusCode(500, $"error: {ex.Message}");
    }
}
}

