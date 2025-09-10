using ScreenTracker1.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace ScreenTracker1.Services
{
    public class RegisterService
    {
        private readonly HttpClient _httpClient;

        public RegisterService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<string> RegisterAsync(RegisterModel registerModel)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{App.URL}Auth/register", registerModel);


                if (response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        return "Registration successful";
                    }
                    else if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        return "You are already registered.";
                    }
                }

                else if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    return "Registration failed. Please check input values.";
                }


                return "Registration failed. Please try again.";
            }
            catch (Exception ex)
            {

                return $"An error occurred: {ex.Message}";
            }
        }



     

    }
}
