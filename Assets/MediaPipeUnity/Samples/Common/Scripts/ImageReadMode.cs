using System.ComponentModel;

namespace Mediapipe.Unity
{
  [System.Serializable]
  public enum ImageReadMode
  {
    CPU,
    [Description("CPU Async")]
    CPUAsync,
    GPU,
  }
}
