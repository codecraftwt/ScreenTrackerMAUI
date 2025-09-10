using Microsoft.JSInterop;
using System.Net.Http.Json;

public class ImageService
{
    private readonly HttpClient _httpClient;

    public ImageService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetImageBlobUrlAsync(string filename, IJSRuntime js)
    {
     
        var bytes = await _httpClient.GetByteArrayAsync($"http://10.0.3.215:85/api/image/file?filename={filename}");

        var jsCode = $@"
            const blob = new Blob([new Uint8Array({System.Text.Json.JsonSerializer.Serialize(bytes)})]);
            const url = URL.createObjectURL(blob);
            localStorage.setItem('{filename}', url);
            url;
        ";

        return await js.InvokeAsync<string>("eval", jsCode);
    }
}
