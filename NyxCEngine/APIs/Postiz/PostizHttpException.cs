namespace NyxCEngine.APIs.Postiz
{
  internal sealed class PostizHttpException : Exception
  {
    public int StatusCode { get; }
    public string ResponseBody { get; }

    public PostizHttpException(int statusCode, string message, string responseBody) : base(message)
    {
      StatusCode = statusCode;
      ResponseBody = responseBody ?? "";
    }

    public override string ToString() => $"{Message}\nStatusCode: {StatusCode}\nBody:\n{ResponseBody}";
  }
}
