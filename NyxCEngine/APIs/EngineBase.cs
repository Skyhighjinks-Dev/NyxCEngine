using Microsoft.Extensions.DependencyInjection;

namespace NyxCEngine.APIs
{
  public abstract class EngineBase
  {
    protected string ClientID { get; set; }
    protected HttpClient _httpClient { get; private set; }


    protected EngineBase(string clientId, IServiceProvider serviceProvider)
    { 
      if(string.IsNullOrEmpty(clientId))
        throw new ArgumentNullException(nameof(clientId));

      this.ClientID = clientId;
      _httpClient = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientID);
    }
  }
}
