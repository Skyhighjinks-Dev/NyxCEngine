namespace NyxCEngine.APIs.ElevenLabs
{
  internal class ElevenLabsEngine : EngineBase
  {
    public ElevenLabsEngine(IServiceProvider serviceProvider) : base(Program.ElevenLabsClientName, serviceProvider)
    { 
    }
  }
}
