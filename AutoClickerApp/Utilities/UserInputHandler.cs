using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public class UserInputHandler
{
  public void SendLeftClick()
  {
    var inputs = new INPUT[2];

    // Mouse down
    inputs[0] = new INPUT
    {
      type = INPUT_MOUSE,
      u = new InputUnion
      {
        mi = new MOUSEINPUT
        {
          dwFlags = MOUSEEVENTF_LEFTDOWN
        }
      }
    };

    // Mouse up
    inputs[1] = new INPUT
    {
      type = INPUT_MOUSE,
      u = new InputUnion
      {
        mi = new MOUSEINPUT
        {
          dwFlags = MOUSEEVENTF_LEFTUP
        }
      }
    };

    SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
  }

  // Interop structures and definitions

  private const uint INPUT_MOUSE = 0;
  private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
  private const uint MOUSEEVENTF_LEFTUP = 0x0004;

  [DllImport("user32.dll", SetLastError = true)]
  private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

  [StructLayout(LayoutKind.Sequential)]
  private struct INPUT
  {
    public uint type;
    public InputUnion u;
  }

  [StructLayout(LayoutKind.Explicit)]
  private struct InputUnion
  {
    [FieldOffset(0)] public MOUSEINPUT mi;
  }

  [StructLayout(LayoutKind.Sequential)]
  private struct MOUSEINPUT
  {
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public UIntPtr dwExtraInfo;
  }
}
