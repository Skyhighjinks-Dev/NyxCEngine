using NyxCEngine.APIs.Postiz.Models;
using NyxCEngine.APIs.Postiz.Models.Integrations;

namespace NyxCEngine.Util.Builders
{
  public static class PostizBuilders
  {
    public static PostItemDto BuildPostItem(
          string integrationId,
          string content,
          UploadAssetDto upload,
          object settings)
      {
        return new PostItemDto
        {
          Integration = new IntegrationRefWithIdentifier { Id = integrationId },
          Value = new List<PostValueDto>
          {
            new PostValueDto
            {
              Content = content,
              Image = new List<UploadRefDto>
              {
                new UploadRefDto { Id = upload.Id!, Path = upload.Path! }
              }
            }
          },
          Settings = settings
        };
      }

    public static object MakeSettings(string platform, string title)
      => platform switch
      {
        "youtube" => new YouTubeSettings { Title = title },
        "tiktok" => new TikTokSettings { Title = title },
        "instagram" => new InstagramSettings { PostType = "post" },
        _ => new { }
      };
  }
}