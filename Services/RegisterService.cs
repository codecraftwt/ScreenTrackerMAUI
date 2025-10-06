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
                else 
                {
                    var errorContent = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == HttpStatusCode.Conflict)
                    {
                      
                        const string key = "\"message\":";
                        int startIndex = errorContent.IndexOf(key);

                        if (startIndex != -1)
                        {
                          
                            startIndex = errorContent.IndexOf('"', startIndex + key.Length) + 1;

                            int endIndex = errorContent.IndexOf('"', startIndex);

                            if (endIndex > startIndex)
                            {
                              
                                string specificMessage = errorContent.Substring(startIndex, endIndex - startIndex);

                          
                                return specificMessage;
                            }
                        }

                   
                        return "Registration failed due to a unique conflict.";
                    }

                    else if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        return "Registration failed. Please check input values.";
                    }

                    return $"Registration failed. Server returned status: {(int)response.StatusCode}.";
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
