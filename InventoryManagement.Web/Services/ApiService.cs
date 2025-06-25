using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace InventoryManagement.Web.Services
{
    public class ApiService:IApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;
        public ApiService(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
            _httpClient.BaseAddress = new Uri(_configuration["ApiGateway:BaseUrl"] ?? "http://localhost:5000");
        }
        private void AddAuthorizationHeader()
        {
            var token=_httpContextAccessor.HttpContext?.Session.GetString("JwtToken");
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization=
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<T?> GetAsync<T>(string endpoint)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.GetAsync(endpoint);

                if (response.IsSuccessStatusCode)
                {
                    var content= await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<T>(content);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        public async Task<TResponse?> PostAsync<TRequest,TResponse>(string endpoint, TRequest data)
        {
            try
            {
                AddAuthorizationHeader();

                var json=JsonConvert.SerializeObject(data);
                var content= new StringContent(json, Encoding.UTF8, "application/json");

                var response=await _httpClient.PostAsync(endpoint, content);

                if(response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TResponse>(responseContent);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        public async Task<TResponse?> PostFormAsync<TResponse>(string endpoint,IFormCollection form)
        {
            try
            {
                AddAuthorizationHeader();

                using var content = new MultipartFormDataContent();

                foreach(var field in form)
                {
                    if (field.Key != "__RequestVerificationToken")
                    {
                        content.Add(new StringContent(field.Value), field.Key);
                    }
                }

                foreach(var file in form.Files)
                {
                    var streamContent = new StreamContent(file.OpenReadStream());
                    streamContent.Headers.ContentType=new MediaTypeHeaderValue(file.ContentType);
                    content.Add(streamContent, file.Name, file.FileName);
                }

                var response= await _httpClient.PostAsync(endpoint, content);

                if(response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TResponse>(responseContent);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }


        public async Task<TResponse?> PutAsync<TRequest,TResponse>(string endpoint, TRequest data)
        {
            try
            {
                AddAuthorizationHeader();
                var json=JsonConvert.SerializeObject(data);
                var content=new StringContent(json, Encoding.UTF8, "application/json");
                var response=await _httpClient.PutAsync(endpoint, content);
                if(response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<TResponse>(responseContent);
                }

                return default;
            }
            catch
            {
                return default;
            }
        }

        public async Task<bool> DeleteAsync(string endpoint)
        {
            try
            {
                AddAuthorizationHeader();
                var response = await _httpClient.DeleteAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}