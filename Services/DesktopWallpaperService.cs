using System;
using System.Runtime.InteropServices;

namespace YouTubeLiveDesktop.Services
{
    /// <summary>
    /// Windows의 Progman/WorkerW 창 구조를 이용해 지정한 창을
    /// 바탕화면 아이콘 뒤(배경 레이어)에 자식 창으로 삽입합니다 (Desktop Layer).
    ///
    /// 참고: Explorer/Windows 버전에 따라 WorkerW가 생성되는 방식이 달라질 수 있습니다.
    /// 아래 로직은 널리 알려진 두 가지 경우(WorkerW가 별도로 생성되는 경우 / 별도
    /// WorkerW를 찾지 못해 Progman을 직접 사용하는 경우)를 모두 처리하는 폴백을 포함합니다.
    /// Windows 대형 업데이트 이후 동작이 달라지면 이 서비스만 재검증하면 됩니다.
    /// </summary>
    public class DesktopWallpaperService
    {
        private const uint WM_SPAWN_WORKER = 0x052C;
        private const int GWL_STYLE = -16;
        private const long WS_CHILD = 0x40000000L;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private IntPtr _target = IntPtr.Zero;

        /// <summary>
        /// 지정한 창 핸들을 바탕화면 배경 레이어(WorkerW 또는 Progman)의 자식으로 삽입합니다.
        /// </summary>
        /// <returns>삽입에 성공하면 true.</returns>
        public bool Embed(IntPtr windowHandle)
        {
            try
            {
                IntPtr progman = FindWindow("Progman", null);
                if (progman == IntPtr.Zero) return false;

                // Progman에게 WorkerW를 생성하도록 요청합니다 (아이콘 레이어와 배경 레이어를 분리).
                SendMessageTimeout(progman, WM_SPAWN_WORKER, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);

                IntPtr workerw = IntPtr.Zero;

                EnumWindows((hwnd, _) =>
                {
                    IntPtr shellView = FindWindowEx(hwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (shellView != IntPtr.Zero)
                    {
                        // 바탕화면 아이콘(SHELLDLL_DefView)을 담고 있는 창의
                        // "다음 형제" WorkerW가 아이콘 뒤 배경 레이어입니다.
                        workerw = FindWindowEx(IntPtr.Zero, hwnd, "WorkerW", null);
                    }
                    return true; // 계속 열거
                }, IntPtr.Zero);

                // 일부 Windows 빌드에서는 위 방식으로 WorkerW를 찾지 못할 수 있어
                // 마지막으로 생성된 WorkerW를 한 번 더 탐색하는 폴백을 둡니다.
                if (workerw == IntPtr.Zero)
                {
                    workerw = FindWindowEx(IntPtr.Zero, IntPtr.Zero, "WorkerW", null);
                }

                // 그래도 못 찾으면 Progman 자체를 부모로 사용합니다 (구형 Explorer 동작 대응).
                _target = workerw != IntPtr.Zero ? workerw : progman;

                // 자식 창(WS_CHILD)으로 만들어야 부모 영역 내부에 올바르게 클리핑됩니다.
                IntPtr style = GetWindowLongPtr(windowHandle, GWL_STYLE);
                SetWindowLongPtr(windowHandle, GWL_STYLE, new IntPtr(style.ToInt64() | WS_CHILD));

                SetParent(windowHandle, _target);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 삽입한 창을 화면 크기/위치에 맞춰 재배치합니다 (해상도 변경, 디스플레이 연결/분리 대응).
        /// </summary>
        public void Resize(IntPtr windowHandle, int x, int y, int width, int height)
        {
            SetWindowPos(windowHandle, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
        }
    }
}
