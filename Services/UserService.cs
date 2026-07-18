using ScreenTracker1.DTOS;
using ScreenTracker1.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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
		private bool _isIntentionalLogout;
		private int uid;
		public UserService(HttpClient httpClient)
		{
			_httpClient = httpClient;

		}
		public void SetUserId(int userId)
		{
			_isIntentionalLogout = false;
			Preferences.Remove("isLoggingOut");
			uid = userId;
		}

		public void BeginIntentionalLogout()
		{
			_isIntentionalLogout = true;
			Preferences.Set("isLoggingOut", true);
		}
		public void SetNavigationManager(NavigationManager navigationManager)
		{
			_navigationManager = navigationManager;
		}
		public async Task<bool> TryHeartbeatRecoveryAsync()
		{
			try
			{
				if (_isIntentionalLogout || Preferences.Get("isLoggingOut", false))
					return false;

				Console.WriteLine("[UserService] Attempting session recovery via heartbeat...");

				var token = Preferences.Get("authToken", "");
				if (string.IsNullOrEmpty(token))
				{
					Console.WriteLine("[UserService] No token found for recovery.");
					return false;
				}

				var request = new HttpRequestMessage(HttpMethod.Post, $"{App.URL}Auth/heartbeat");
				request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
				var response = await _httpClient.SendAsync(request);

				if (response.IsSuccessStatusCode)
				{
					Console.WriteLine("[UserService] Heartbeat recovery succeeded.");
					return true;
				}

				if (response.StatusCode == HttpStatusCode.Unauthorized)
				{
					Console.WriteLine("[UserService] Heartbeat 401, trying token refresh...");

					var refreshPayload = System.Text.Json.JsonSerializer.Serialize(new { token });
					var refreshContent = new StringContent(refreshPayload, System.Text.Encoding.UTF8, "application/json");
					var refreshResponse = await _httpClient.PostAsync($"{App.URL}auth/refresh-token", refreshContent);

					if (refreshResponse.IsSuccessStatusCode)
					{
						if (_isIntentionalLogout || Preferences.Get("isLoggingOut", false))
							return false;

						var body = await refreshResponse.Content.ReadAsStringAsync();
						using var doc = JsonDocument.Parse(body);
						string? newToken = null;
						if (doc.RootElement.TryGetProperty("token", out var t))
							newToken = t.GetString();
						else if (doc.RootElement.TryGetProperty("accessToken", out var at))
							newToken = at.GetString();
						else if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object)
						{
							if (d.TryGetProperty("token", out var dt))
								newToken = dt.GetString();
						}

						if (!string.IsNullOrEmpty(newToken))
						{
							if (_isIntentionalLogout || Preferences.Get("isLoggingOut", false))
								return false;

							Preferences.Set("authToken", newToken);
							Console.WriteLine("[UserService] Token refreshed during recovery. Verifying with heartbeat...");

							var verifyRequest = new HttpRequestMessage(HttpMethod.Post, $"{App.URL}Auth/heartbeat");
							verifyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", newToken);
							var verifyResponse = await _httpClient.SendAsync(verifyRequest);

							if (verifyResponse.IsSuccessStatusCode)
							{
								Console.WriteLine("[UserService] Recovery heartbeat OK after token refresh.");
								return true;
							}
						}
					}
				}

				Console.WriteLine("[UserService] Session recovery failed.");
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[UserService] Session recovery error: {ex.Message}");
				return false;
			}
		}

		private async void HandleUnauthorized()
		{
			if (_isPopupShown || _isIntentionalLogout) return;

			bool recovered = await TryHeartbeatRecoveryAsync();
			if (_isIntentionalLogout) return;
			if (recovered)
			{
				Console.WriteLine("[UserService] 401 suppressed — session recovered (Mac sleep wake).");
				return;
			}

			_isPopupShown = true;
			_isIntentionalLogout = true;
			Preferences.Set("isLoggingOut", true);
			Preferences.Remove("authToken");
			Preferences.Remove("auth_token");
			SecureStorage.Remove("authToken");
			SecureStorage.Remove("auth_token");
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

        public async Task<bool> UpdateUserAsync(User user, int updatedBy)
        {
            var url = $"{App.URL}User/update?updatedBy={updatedBy}";

            var request = new HttpRequestMessage(HttpMethod.Put, url);
            AddAuthorizationHeader(request);

            // No need for 'userForBody'. 
            // Serializing 'user' directly uses your [JsonPropertyName] attributes.
            var jsonContent = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(user),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            request.Content = jsonContent;
            var response = await _httpClient.SendAsync(request);

            return response.IsSuccessStatusCode;
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

        public async Task<bool> PromoteUserToAdminAsync(int targetUserId, int authorizedBy)
        {
            try
            {
                var requestUrl = $"{App.URL}User/promoteToAdmin?targetUserId={targetUserId}&authorizedBy={authorizedBy}";

                var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
                AddAuthorizationHeader(request);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    HandleUnauthorized();
                }

                Console.WriteLine($"Error promoting user to admin: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception promoting user to admin: {ex.Message}");
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
				var responseString = await response.Content.ReadAsStringAsync();

				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"Error starting daily tracker: HTTP {(int)response.StatusCode} {response.StatusCode}: {responseString}");
					return null;
				}

			
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

        public async Task<List<AppUsageData>> GetAppUsageDataAsync(int id, string usageType, string userRole)
        {
            try
            {
              
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

        /// <summary>
        /// Fetches ALL app title records for a user via AppTitle/user/{userId} (not date-filtered).
        /// This is a frontend-only workaround for the broken AppTitle/day/{date}/{id} endpoint.
        /// Filtering by the selected date is done client-side.
        /// Falls back to in-memory tracker data if this endpoint also fails.
        /// </summary>
        public async Task<List<AppTitle>> GetAllAppTitlesByUserAsync(int userId)
        {
            try
            {
                var url = $"{App.URL}AppTitle/user/{userId}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthorizationHeader(request);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"GetAllAppTitlesByUserAsync: HTTP {response.StatusCode}");
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
                Console.WriteLine($"Error fetching all app titles by user: {ex.Message}");
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

        //public async Task<List<Screenshots>> GetImagesByDateAsync(int userId, DateTime date, int skip = 1, int take = 6, string usageType = "all", string startTime = null, string endTime = null)
        //{
        //	try
        //	{
        //		string formattedDate = date.ToString("yyyy-MM-dd");
        //		string url = $"{App.URL}Image/by-date?userId={userId}&date={formattedDate}&skip={skip}&take={take}&usageType={usageType}";

        //		// Add time filter parameters if provided
        //		if (!string.IsNullOrEmpty(startTime))
        //		{
        //			url += $"&startTime={startTime}";
        //		}
        //		if (!string.IsNullOrEmpty(endTime))
        //		{
        //			url += $"&endTime={endTime}";
        //		}

        //		Console.WriteLine($"Request URL: {url}");

        //		var request = new HttpRequestMessage(HttpMethod.Get, url);
        //		AddAuthorizationHeader(request);

        //		var response = await _httpClient.SendAsync(request);

        //		if (!response.IsSuccessStatusCode)
        //		{
        //			Console.WriteLine($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        //			return new List<Screenshots>();
        //		}

        //		var responseString = await response.Content.ReadAsStringAsync();
        //		var images = JsonSerializer.Deserialize<List<Screenshots>>(responseString);

        //		return images ?? new List<Screenshots>();
        //	}
        //	catch (Exception ex)
        //	{
        //		Console.WriteLine($"Error fetching images: {ex.Message}");
        //		return new List<Screenshots>();
        //	}
        //}


        public async Task<List<Screenshots>> GetImagesByDateAsync(int userId, DateTime date, int skip = 1, int take = 6, string usageType = "all", string startTime = null, string endTime = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // IMPORTANT: Database stores captureTime as timestamptz WITH the local
                // timezone offset (+05:30 IST). Send the LOCAL date as-is — do NOT
                // convert to UTC here, the timestamps are not stored in UTC.
                string formattedDate = date.ToString("yyyy-MM-dd");
                string url = $"{App.URL}Image/by-date?userId={userId}&date={formattedDate}&skip={skip}&take={take}&usageType={usageType}";

                if (!string.IsNullOrEmpty(startTime))
                    url += $"&startTime={Uri.EscapeDataString(startTime)}";
                if (!string.IsNullOrEmpty(endTime))
                    url += $"&endTime={Uri.EscapeDataString(endTime)}";

                Console.WriteLine($"[GetImagesByDateAsync] Request URL: {url}");
                Console.WriteLine($"[GetImagesByDateAsync] Date: {formattedDate} (local, not UTC)");

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthorizationHeader(request);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Console.WriteLine($"[GetImagesByDateAsync] Response status: {response.StatusCode}, body length: {responseBody?.Length ?? 0}");
                if (responseBody?.Length > 0 && responseBody?.Length < 500)
                    Console.WriteLine($"[GetImagesByDateAsync] Response body: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[GetImagesByDateAsync] HTTP {response.StatusCode}: {responseBody}");
                    return new List<Screenshots>();
                }

                var images = JsonSerializer.Deserialize<List<Screenshots>>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                Console.WriteLine($"[GetImagesByDateAsync] Deserialized {images?.Count ?? 0} screenshots");
                return images ?? new List<Screenshots>();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("[GetImagesByDateAsync] Request was cancelled (timeout)");
                return new List<Screenshots>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetImagesByDateAsync] Error: {ex.Message}");
                return new List<Screenshots>();
            }
        }



        public async Task<bool> DeleteScreenshotAsync(int screenshotId)
		{
			try
			{
				string url = $"{App.URL}Image/{screenshotId}";
				var request = new HttpRequestMessage(HttpMethod.Delete, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					var errorBody = await response.Content.ReadAsStringAsync();
					Console.WriteLine($"HTTP{response.StatusCode}:{errorBody}");
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

		public async Task<bool> UpdateManualTrackingAuthAsync(int id, int? manualTrackingAuthBy, bool isManualTrackingEnabled)
		{
			try
			{
				string requestUrl = $"{App.URL}User/updateManualTrackingAuth?id={id}&manualTrackingAuthBy={manualTrackingAuthBy}&isManualTrackingEnabled={isManualTrackingEnabled}";

				var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);

				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);
				response.EnsureSuccessStatusCode();

				return response.IsSuccessStatusCode;
			}
			catch (HttpRequestException ex)
			{
				Console.WriteLine($"Error calling updateManualTrackingAuth API: {ex.Message}");
				return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"An unexpected error occurred: {ex.Message}");
				return false;
			}
		}

		public async Task<bool> UpdateAutoTrackingAuthAsync(int id, int? autoTrackingAuthBy, bool isAutoTrackingEnabled)
		{
			try
			{
				string requestUrl = $"{App.URL}User/updateAutoTrackingAuth?id={id}&autoTrackingAuthBy={autoTrackingAuthBy}&isAutoTrackingEnabled={isAutoTrackingEnabled}";

				var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);

				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);
				response.EnsureSuccessStatusCode();

				return response.IsSuccessStatusCode;
			}
			catch (HttpRequestException ex)
			{
				Console.WriteLine($"Error calling updateAutoTrackingAuth API: {ex.Message}");
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

        public async Task<List<User>> GetActiveUsersAsync(int? adminId = null)
        {
            try
            {
                var url = $"{App.URL}DailyTracker/ActiveUser";

                if (adminId.HasValue)
                {
                    url += $"?adminId={adminId.Value}";
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

        public async Task LogoutAsync()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{App.URL}Auth/logout");
                AddAuthorizationHeader(request);
                await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling logout API: {ex.Message}");
            }
        }

        public async Task HeartbeatAsync()
        {
            try
            {
                if (_isIntentionalLogout || Preferences.Get("isLoggingOut", false))
                    return;

                var request = new HttpRequestMessage(HttpMethod.Post, $"{App.URL}Auth/heartbeat");
                AddAuthorizationHeader(request);
                await _httpClient.SendAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling heartbeat API: {ex.Message}");
            }
        }

		public async Task<List<User>> GetActiveSessionsAsync(int? adminId = null, bool suppressUnauthorized = false)
		{
			try
			{
				var url = $"{App.URL}Auth/active-sessions";

				if (adminId.HasValue)
				{
					url += $"?adminId={adminId.Value}";
				}

				var request = new HttpRequestMessage(HttpMethod.Get, url);
				AddAuthorizationHeader(request);

				var response = await _httpClient.SendAsync(request);

				if (!response.IsSuccessStatusCode)
				{
					if (response.StatusCode == HttpStatusCode.Unauthorized && !suppressUnauthorized)
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
				Console.WriteLine($"Error fetching active sessions: {ex.Message}");
				return new List<User>();
			}
		}



        public async Task<PagedResult<DailyTrackerWithUserDto>> GetAllUsersReportAsync(
	  DateTime fromDate,
	  DateTime toDate,
	  string searchString,
	  int? adminId,
	  int pageNumber = 1,
	  int pageSize = 10,
	  string usageType = "all")
		{
			try
			{
				var url = $"{App.URL}DailyTracker/allUsersreport?" +
						  $"fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}" +
						  $"&searchString={searchString}&pageNumber={pageNumber}&pageSize={pageSize}" +
						  $"&usageType={usageType}";

				// Add adminId parameter only if it has a value (for filtering by specific user)
				if (adminId.HasValue)
				{
					url += $"&adminId={adminId.Value}";
				}

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


		public async Task<DateTime?>GetTodaysStartTrackerAsync(int userId)
		{
			try
			{
				var response = await _httpClient.GetAsync($"{App.URL}DailyTracker/today/{userId}");
				if(response.StatusCode == HttpStatusCode.Unauthorized)
				{
					HandleUnauthorized();
					return null ;
				}
				if(!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"API Error:{response.StatusCode}");
					return null ;
				}
				var json = await response.Content.ReadAsStringAsync();
				if(string.IsNullOrWhiteSpace(json))
				{
					return null;
				}
				var tracker = JsonSerializer.Deserialize<DailyTrackerDto>(json,new JsonSerializerOptions { PropertyNameCaseInsensitive=true });
				return tracker?.StartTracker;

			}
			catch(Exception e)
			{
				Console.WriteLine(e);
				return null ;
			}
		}

        public async Task<DailyTrackerAggregateResponse> GetDailyTrackerAggregateAsync(int userId, DateTime date, string startMode)
        {
            // Format date to ensure correct parsing by the API, e.g., "2025-10-14"
            string dateStr = date.ToString("yyyy-MM-dd");

            // Assuming the controller name is DailyTracker
            string url = $"{App.URL}DailyTracker/aggregate?userId={userId}&date={dateStr}&startMode={startMode}";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                AddAuthorizationHeader(request);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<DailyTrackerAggregateResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    return result ?? new DailyTrackerAggregateResponse();

                    return result ?? new DailyTrackerAggregateResponse();
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    HandleUnauthorized();
                    return new DailyTrackerAggregateResponse();
                }
                else
                {
                    Console.WriteLine($"Error fetching aggregate data: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
                    return new DailyTrackerAggregateResponse();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception fetching aggregate data: {ex.Message}");
                return new DailyTrackerAggregateResponse();
            }
        }

        /// <summary>
        /// Fetches an image from a URL via .NET HttpClient and returns it as a base64 data URI.
        /// Use this on Mac/iOS where WKWebView blocks http:// image src attributes.
        /// </summary>
        public async Task<string> FetchImageAsBase64Async(string imageUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(imageUrl))
                    return string.Empty;

                var bytes = await _httpClient.GetByteArrayAsync(imageUrl);
                string base64 = Convert.ToBase64String(bytes);
                return $"data:image/png;base64,{base64}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FetchImageAsBase64Async] Error fetching {imageUrl}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
