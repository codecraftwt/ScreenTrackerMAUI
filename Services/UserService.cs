using ScreenTracker1.DTOS;
using ScreenTracker1.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using System.Text;

namespace ScreenTracker1.Services
{
	public class UserService
	{
		private readonly HttpClient _httpClient;
		private NavigationManager? _navigationManager;
		private bool _isPopupShown = false;
		private int uid;
		public UserService(HttpClient httpClient)
		{
			_httpClient = httpClient;

		}
		public void SetUserId(int userId)
		{
			uid = userId;
		}
		public void SetNavigationManager(NavigationManager navigationManager)
		{
			_navigationManager = navigationManager;
		}
		private async void HandleUnauthorized()
		{
			if (_isPopupShown) return;

			_isPopupShown = true;
			Preferences.Remove("authToken");
			SecureStorage.RemoveAll();
			await MainThread.InvokeOnMainThreadAsync(async () =>
			{
				await App.Current.MainPage.DisplayAlert(
					"Session Expired",
					"You have logged in from another device. Please log in again.",
					"OK"
				);

				_navigationManager?.NavigateTo("/login", forceLoad: true);

				_isPopupShown = false;
			});
		}
        public async Task<bool> DeleteUserAsync(int userId)
        {
            try
            {
                
                var response = await _httpClient.DeleteAsync($"{App.URL}User/{userId}");

        
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception deleting user: {ex.Message}");
                return false;
            }
        }
        public async Task<bool> UpdateActiveStatusAsync(int userId, int updatedBy, bool isActive)
        {
            try
            {
                var requestUrl = $"{App.URL}User/updateActiveStatus?id={userId}&updatedBy={updatedBy}&isActive={isActive}";

                var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
                AddAuthorizationHeader(request);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    HandleUnauthorized();
                    return false;
                }
                else
                {
                    Console.WriteLine($"Error updating active status: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception updating active status: {ex.Message}");
                return false;
            }
        }


        public async Task<int> GetDayUsageCountAsync(DateTime date, int id, string usageType = "all")
		{
			try
			{
			
				var formattedDate = date.ToString("yyyy-MM-dd");

			
				var url = $"{App.URL}AppUsage/day/{formattedDate}/{id}/count?usageType={usageType}";

			
				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);
				var response = await _httpClient.SendAsync(request);
				if (response.IsSuccessStatusCode)
				{
					var responseString = await response.Content.ReadAsStringAsync();
					if (int.TryParse(responseString, out int count))
					{
						return count;
					}
					else
					{
						Console.WriteLine("Failed to parse response as integer.");
						return 0;
					}
				}
				else if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
				
					HandleUnauthorized();
					return 0;
				}
				else
				{
				
					Console.WriteLine($"Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
					return 0;
				}
			}
			catch (Exception ex)
			{
				
				Console.WriteLine($"Exception while fetching app usage count: {ex.Message}");
				return 0;
			}
		}

		public async Task<string?> GetUserLoginTimeFormattedAsync(int userId)
		{
			try
			{
				
				var url = $"{App.URL}Auth/Users/{userId}/LoginTime";
				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request); 
				var response = await _httpClient.SendAsync(request);
				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return null;
				}

				var responseString = await response.Content.ReadAsStringAsync();
				return responseString;
			}
			catch (Exception ex)
			{
			
				Console.WriteLine($"Error fetching user login time: {ex.Message}");
				return null;
			}
		}

		private void AddAuthorizationHeader(HttpRequestMessage request)
		{
			string? token = Preferences.Get("authToken", "");
			if (!string.IsNullOrWhiteSpace(token))
			{
				request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
			}
			else
			{
				Console.WriteLine("JWT token is missing. User might not be logged in.");
			}
		}

		public async Task<List<User>> GetAllUsersAsync()
		{
			try
			{
				var request = new HttpRequestMessage(HttpMethod.Get, $"{App.URL}User/allUsers");
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<User>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var users = JsonSerializer.Deserialize<List<User>>(responseString);
				return users ?? new List<User>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching users: {ex.Message}");
				return new List<User>();
			}
		}



		public async Task<DailyTracker> StartDailyTrackerAsync(string startMode = "automatic")
		{
			try
			{
				var url = $"{App.URL}DailyTracker/start";
				var request = new HttpRequestMessage(HttpMethod.Post, url);


				var tracker = new DailyTracker
				{
					UserId = uid,  
					StartMode = startMode  
				};

				var jsonContent = new StringContent(JsonSerializer.Serialize(tracker), Encoding.UTF8, "application/json");

				AddAuthorizationHeader(request);

				request.Content = jsonContent;

				
				var response = await _httpClient.SendAsync(request);

				
				response.EnsureSuccessStatusCode();

				
				var responseString = await response.Content.ReadAsStringAsync();

			
				var dailyTracker = JsonSerializer.Deserialize<DailyTracker>(responseString);

				return dailyTracker;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error starting daily tracker: {ex.Message}");
				return null;
			}
		}




		public async Task<List<AppUsage>> GetAppUsageByUserIdAsync(int userId, DateTime date, int page = 1, int take = 5, string usageType = "all", string userRole = "user")
		{
			try
			{
				string formattedDate = date.ToString("yyyy-MM-dd");
			
				string url = $"{App.URL}AppUsage/day/{formattedDate}/{userId}?page={page}&take={take}&usageType={usageType}&userRole={userRole}";

				Console.WriteLine($"Calling URL: {url}"); 

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);
				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						HandleUnauthorized();
					}

					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<AppUsage>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var appUsageList = JsonSerializer.Deserialize<List<AppUsage>>(responseString, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return appUsageList ?? new List<AppUsage>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching app usage data: {ex.Message}");
				return new List<AppUsage>();
			}
		}



        // Change signature to accept usageType
        public async Task<List<AppUsageData>> GetAppUsageDataAsync(int id, string usageType, string userRole)
        {
            try
            {
                // Append both usageType and userRole as query string parameters
                var url = $"{App.URL}AppUsage/lastDaysTotal/{id}?usageType={usageType}&userRole={userRole}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthorizationHeader(request);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    HandleUnauthorized();
                    Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                    return new List<AppUsageData>();
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var appUsageData = JsonSerializer.Deserialize<List<AppUsageData>>(responseString);
                return appUsageData ?? new List<AppUsageData>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching app usage data with ID {id}: {ex.Message}");
                return new List<AppUsageData>();
            }
        }

        public async Task<List<AppTitle>> GetAppTitleByDateAsync(DateTime date, int id, int page = 1, int take = 5)
		{
			try
			{
				string formattedDate = date.ToString("yyyy-MM-dd");
				var url = $"{App.URL}AppTitle/day/{formattedDate}/{id}?page={page}&take={take}";

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					HandleUnauthorized();
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<AppTitle>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var appTitleList = JsonSerializer.Deserialize<List<AppTitle>>(responseString);
				return appTitleList ?? new List<AppTitle>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching app title data: {ex.Message}");
				return new List<AppTitle>();
			}
		}

		public async Task<List<CategoryKeywordGroup>> GetGroupedCategoryKeywordsAsync(int id, DateTime date, int page = 1, int take = 5)
		{
			try
			{
				string formattedDate = date.ToString("yyyy-MM-dd");
				var url = $"{App.URL}CategoryKeyword/groupedCategory/{id}/{formattedDate}?page={page}&take={take}";

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					HandleUnauthorized();
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<CategoryKeywordGroup>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var groupedKeywords = JsonSerializer.Deserialize<List<CategoryKeywordGroup>>(responseString);
				return groupedKeywords ?? new List<CategoryKeywordGroup>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching grouped category keywords: {ex.Message}");
				return new List<CategoryKeywordGroup>();
			}
		}

		public async Task<List<AppUsage>> GetAppUsageByUserIdAsyncuser(int userId, DateTime startDate, int page = 1, int take = 5)
		{
			try
			{
				string formattedDate = startDate.ToString("yyyy-MM-dd");
				var url = $"{App.URL}AppUsage/user/usage?startDate={formattedDate}&page={page}&take={take}";


				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					HandleUnauthorized();
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<AppUsage>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var appUsageList = JsonSerializer.Deserialize<List<AppUsage>>(responseString);
				return appUsageList ?? new List<AppUsage>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching app usage data: {ex.Message}");
				return new List<AppUsage>();
			}
		}

		public async Task<List<AppTitle>> GetAppTitleDetailsAsync(DateTime date, int userId, string appName, int page = 1, int take = 5)
		{
			try
			{
				string formattedDate = date.ToString("yyyy-MM-dd");
				var encodedAppName = Uri.EscapeDataString(appName);
				string url = $"{App.URL}AppTitle/AppDetails/{formattedDate}/{userId}?appName={encodedAppName}&page={page}&take={take}";

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						HandleUnauthorized();
					}

					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<AppTitle>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var appTitleList = JsonSerializer.Deserialize<List<AppTitle>>(responseString, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return appTitleList ?? new List<AppTitle>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching app title details: {ex.Message}");
				return new List<AppTitle>();
			}
		}

		public async Task<List<CategoryKeywordGroup>> GetCategoryByAppNameAsync(string appName)
		{
			try
			{
				var encodedAppName = Uri.EscapeDataString(appName);

				var url = $"{App.URL}CategoryKeyword/Category?Keyword={encodedAppName}";

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					HandleUnauthorized();
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<CategoryKeywordGroup>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var result = JsonSerializer.Deserialize<List<CategoryKeywordGroup>>(responseString, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return result ?? new List<CategoryKeywordGroup>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching category by app name: {ex.Message}");
				return new List<CategoryKeywordGroup>();
			}
		}

		public async Task<List<Screenshots>> GetImagesByDateAsync(int userId, DateTime date, int skip = 1, int take = 6, string usageType = "all")
		{
			try
			{
				string formattedDate = date.ToString("yyyy-MM-dd");
				string url = $"{App.URL}Image/by-date?userId={userId}&date={formattedDate}&skip={skip}&take={take}&usageType={usageType}";

				Console.WriteLine($"Request URL: {url}");

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<Screenshots>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var images = JsonSerializer.Deserialize<List<Screenshots>>(responseString);

				return images ?? new List<Screenshots>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching images: {ex.Message}");
				return new List<Screenshots>();
			}
		}






		public async Task<bool> DeleteScreenshotAsync(int screenshotId)
		{
			try
			{
				string url = $"{App.URL}Image/{screenshotId}";
				var request = new HttpRequestMessage(HttpMethod.Delete, url);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"HTTP{response.StatusCode}:{await response.Content.ReadAsStringAsync()}");
					return false;
				}
				return true;

			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error deleting screenshot: {ex.Message}");
				return false;
			}
		}

		public async Task<double> GetAfkLogsTotalAsync(
	  int userId,
	  DateTime date,
	  string userRole,
	  string startMode = "all")
		{
			try
			{
				string formattedDate = date.ToString("yyyy-MM-dd");

				if (userRole.ToLower() == "user")
				{
					startMode = "manual";
				}

				string url = $"{App.URL}AfkLogs/total?userId={userId}&date={formattedDate}&mode={startMode}";

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						HandleUnauthorized();
					}

					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return 0;
				}

				var responseString = await response.Content.ReadAsStringAsync();
				string timeText = responseString.Trim('"');

				if (TimeSpan.TryParse(timeText, out TimeSpan ts))
				{
					return ts.TotalMinutes;
				}

				return 0;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching AFK total logs: {ex.Message}");
				return 0;
			}
		}


		public async Task<bool> UpdateDeleteAuthAsync(int id, int? deleteAuthBy, bool isSelected)
		{
			try
			{

				string requestUrl = $"{App.URL}User/updateDeleteAuth?id={id}&deleteAuthBy={deleteAuthBy}&isSelected={isSelected}";

				var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);


				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);
				response.EnsureSuccessStatusCode();

				return response.IsSuccessStatusCode;
			}
			catch (HttpRequestException ex)
			{
				Console.WriteLine($"Error calling updateDeleteAuth API: {ex.Message}");
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An unexpected error occurred: {ex.Message}");
				return false;
			}
		}

		public async Task<User> GetUserByIdAsync(int id)
		{
			try
			{
				string url = $"{App.URL}User/{id}";
				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (response.StatusCode == HttpStatusCode.NotFound)
				{
					Console.WriteLine($"User with ID {id} not found.");
					return null;
				}

				response.EnsureSuccessStatusCode();

				var responseString = await response.Content.ReadAsStringAsync();
				var user = JsonSerializer.Deserialize<User>(responseString);

				return user;
			}
			catch (HttpRequestException ex)
			{
				Console.WriteLine($"Error calling User API: {ex.Message}");
				return null;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An unexpected error occurred: {ex.Message}");
				return null;
			}
		}



		public async Task<List<User>> GetAllUsersByAdminAsync(int adminId, string role, string searchTerm = null)
		{
			try
			{
				var url = $"{App.URL}User/allUsersByAdmin?AdminId={adminId}&Role={role}";

				if (!string.IsNullOrEmpty(searchTerm))
				{
					url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
				}

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						HandleUnauthorized();
					}
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<User>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var users = JsonSerializer.Deserialize<List<User>>(responseString, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return users ?? new List<User>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching users by admin and role: {ex.Message}");
				return new List<User>();
			}
		}


        public async Task<List<User>> GetAllUsersByAdminAsyncforsetting(int adminId, string role, string searchTerm = null)
        {
            try
            {
                var url = $"{App.URL}User/allUsersByAdminforsetting?AdminId={adminId}&Role={role}";

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthorizationHeader(request);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        HandleUnauthorized();
                    }
                    Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                    return new List<User>();
                }

                var responseString = await response.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<List<User>>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return users ?? new List<User>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching users by admin and role: {ex.Message}");
                return new List<User>();
            }
        }
        public async Task<Screenshots?> GetRelativeScreenshotAsync(int userId, int currentImageId, DateTime date, string direction, string usageType = "all")
        {
            try
            {
                // Format the date to "yyyy-MM-dd" as expected by the API
                string formattedDate = date.ToString("yyyy-MM-dd");

                // Prepare the URL with usageType
                string url = $"{App.URL}Image/{userId}/{currentImageId}/{formattedDate}/{direction}/{usageType}";

                // Create the HTTP request
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                // Add authorization header, assuming you have a method for that
                AddAuthorizationHeader(request);

                // Send the HTTP request
                var response = await _httpClient.SendAsync(request);

                // If the response is successful
                if (response.IsSuccessStatusCode)
                {
                    // Read the response content as a string
                    var responseString = await response.Content.ReadAsStringAsync();

                    // Check if the response is empty or just an empty JSON object
                    if (string.IsNullOrWhiteSpace(responseString) || responseString == "{}")
                    {
                        return null;
                    }

                    // Deserialize the JSON response to a Screenshots object
                    var screenshot = JsonSerializer.Deserialize<Screenshots>(responseString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    // If screenshot is valid, return it
                    if (screenshot != null && screenshot.id > 0)
                    {
                        return screenshot;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log the error (in production, consider logging this in a file or logging system)
                Console.WriteLine($"Error fetching relative screenshot: {ex.Message}");
                return null;
            }
        }







        public async Task<DailyTracker> EndDailyTrackerAsync()
		{
			try
			{
				var url = $"{App.URL}DailyTracker/end";
				var request = new HttpRequestMessage(HttpMethod.Post, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);
				response.EnsureSuccessStatusCode();

				var responseString = await response.Content.ReadAsStringAsync();
				var dailyTracker = JsonSerializer.Deserialize<DailyTracker>(responseString);
				return dailyTracker;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error ending daily tracker: {ex.Message}");
				return null;
			}
		}

		public async Task<List<User>> GetAdminsWithUsersAsync(int superAdminId)
		{
			try
			{

				var url = $"{App.URL}User/allUsersByAdmin?AdminId={superAdminId}&Role=ad";

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						HandleUnauthorized();
					}

					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<User>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var admins = JsonSerializer.Deserialize<List<User>>(responseString, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return admins ?? new List<User>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching admins for superadmin: {ex.Message}");
				return new List<User>();
			}
		}

		public async Task<List<User>> GetActiveUsersAsync()
		{
			try
			{
				var url = $"{App.URL}DailyTracker/ActiveUser";
				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						HandleUnauthorized();
					}

					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<User>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var activeUsers = JsonSerializer.Deserialize<List<User>>(responseString, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return activeUsers ?? new List<User>();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching active users: {ex.Message}");
				return new List<User>();
			}
		}



		public async Task<PagedResult<DailyTrackerWithUserDto>> GetAllUsersReportAsync(
	  DateTime fromDate,
	  DateTime toDate,
	  string searchString,
	  int adminId,
	  int pageNumber = 1,
	  int pageSize = 10,
	  string usageType = "all")
		{
			try
			{
				var url = $"{App.URL}DailyTracker/allUsersreport?" +
						  $"adminId={adminId}&fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}" +
						  $"&searchString={searchString}&pageNumber={pageNumber}&pageSize={pageSize}" +
						  $"&usageType={usageType}";

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);
				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized)
						HandleUnauthorized();

					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new PagedResult<DailyTrackerWithUserDto>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
				var result = JsonSerializer.Deserialize<PagedResultWrapper<DailyTrackerWithUserDto>>(responseString,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

				return new PagedResult<DailyTrackerWithUserDto>
				{
					Items = result?.Users ?? new List<DailyTrackerWithUserDto>(),
					TotalCount = result?.TotalCount ?? 0
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching report: {ex.Message}");
				return new PagedResult<DailyTrackerWithUserDto>();
			}
		}



		public async Task<PagedResult<DailyTrackerWithUserDto>> GetTodayDailyTrackerAsync(
	  int userId,
	  DateTime fromDate,
	  DateTime toDate,
	  string searchString = "",
	  int pageNumber = 1,
	  int pageSize = 10,
	   string usageType = "all")
		{
			try
			{
				var url = $"{App.URL}DailyTracker/today?" +
				$"userId={userId}&fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}" +
				$"&searchString={searchString}&pageNumber={pageNumber}&pageSize={pageSize}" +
				$"&usageType={usageType}";

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized)
					{
						HandleUnauthorized();
					}
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
				
					return new PagedResult<DailyTrackerWithUserDto>();
				}

				var responseString = await response.Content.ReadAsStringAsync();
			
				var apiResult = JsonSerializer.Deserialize<ApiPagedResult<DailyTrackerWithUserDto>>(responseString, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				return new PagedResult<DailyTrackerWithUserDto>
				{
					Items = apiResult?.Users ?? new List<DailyTrackerWithUserDto>(),
					TotalCount = apiResult?.TotalCount ?? 0
				};
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error fetching daily tracker: {ex.Message}");
				return new PagedResult<DailyTrackerWithUserDto>();
			}
		}

		public async Task<List<DateTime>> GetAvailableDatesAsync(int userId)
		{
			try
			{

				string url = $"{App.URL}Image/available-dates?userId={userId}";


				var request = new HttpRequestMessage(HttpMethod.Get, url);


				AddAuthorizationHeader(request);


				var response = await _httpClient.SendAsync(request);


				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
					return new List<DateTime>();
				}


				var responseString = await response.Content.ReadAsStringAsync();


				var availableDates = JsonSerializer.Deserialize<List<DateTime>>(responseString);


				return availableDates ?? new List<DateTime>();
			}
			catch (Exception ex)
			{

				Console.WriteLine($"Error fetching available dates: {ex.Message}");
				return new List<DateTime>();
			}
		}
	}
}