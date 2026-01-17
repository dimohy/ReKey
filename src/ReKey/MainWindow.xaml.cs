using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Linq;
using Microsoft.Win32;

namespace ReKey;

public partial class MainWindow : Window
{
    private bool _captureFrom;
    private bool _captureTo;
    private KeyInfo? _fromKey;
    private KeyInfo? _toKey;
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _diagnosticEnabled;

    public MainWindow()
    {
        InitializeComponent();
        FromKeyCombo.ItemsSource = KeyOptions;
        ToKeyCombo.ItemsSource = KeyOptions;
        FromKeyCombo.AddHandler(PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(ComboBox_PreviewMouseLeftButtonDown), true);
        ToKeyCombo.AddHandler(PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(ComboBox_PreviewMouseLeftButtonDown), true);
        RefreshMappingList();
        UpdateStatus("원본 키와 대상 키를 선택하세요.");
    }

    protected override void OnClosed(EventArgs e)
    {
        StopDiagnostic();
        base.OnClosed(e);
    }

    private void SelectFrom_Click(object sender, RoutedEventArgs e)
    {
        _captureFrom = true;
        _captureTo = false;
        UpdateStatus("원본 키를 눌러주세요.");
    }

    private void SelectTo_Click(object sender, RoutedEventArgs e)
    {
        _captureFrom = false;
        _captureTo = true;
        UpdateStatus("대상 키를 눌러주세요.");
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_captureFrom && !_captureTo)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var vk = KeyInterop.VirtualKeyFromKey(key);
        var scan = (ushort)MapVirtualKey((uint)vk, MapVkToVscEx);
        var info = new KeyInfo(key.ToString(), vk, scan);

        if (_captureFrom)
        {
            _fromKey = info;
            FromKeyCombo.SelectedItem = null;
            FromKeyCombo.Text = info.ToDisplayString();
        }
        else if (_captureTo)
        {
            _toKey = info;
            ToKeyCombo.SelectedItem = null;
            ToKeyCombo.Text = info.ToDisplayString();
        }

        _captureFrom = false;
        _captureTo = false;
        UpdateStatus("키 선택이 완료되었습니다.");
    }

    private void FromKeyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (FromKeyCombo.SelectedItem is KeyOption option)
        {
            _fromKey = new KeyInfo(option.DisplayName, option.VirtualKey, option.ScanCode);
            UpdateStatus("원본 키가 선택되었습니다.");
        }
    }

    private void ToKeyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ToKeyCombo.SelectedItem is KeyOption option)
        {
            _toKey = new KeyInfo(option.DisplayName, option.VirtualKey, option.ScanCode);
            UpdateStatus("대상 키가 선택되었습니다.");
        }
    }

    private void ComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox comboBox && !comboBox.IsDropDownOpen)
        {
            if (!comboBox.IsKeyboardFocusWithin)
            {
                comboBox.Focus();
            }

            comboBox.IsDropDownOpen = true;
            e.Handled = true;
        }
    }

    private void RefreshMappingList()
    {
        var mappings = LoadMappings();
        if (mappings.Count == 0)
        {
            MappingList.ItemsSource = new[]
            {
                new MappingView(null, "없음")
            };
            return;
        }

        MappingList.ItemsSource = mappings
            .Select(m => new MappingView(
                m,
                $"{GetKeyDisplayName(m.FromScanCode)} → {GetKeyDisplayName(m.ToScanCode)}"))
            .ToList();
    }

    private static IReadOnlyList<Mapping> LoadMappings()
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Keyboard Layout", false);

        if (key?.GetValue("Scancode Map") is not byte[] data || data.Length < 12)
        {
            return Array.Empty<Mapping>();
        }

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var count = reader.ReadUInt32();
        var mappingCount = (int)count - 1;

        if (mappingCount <= 0)
        {
            return Array.Empty<Mapping>();
        }

        var list = new List<Mapping>(mappingCount);
        for (var i = 0; i < mappingCount; i++)
        {
            if (stream.Position + 4 > stream.Length)
            {
                break;
            }

            var entry = reader.ReadUInt32();
            var to = (ushort)(entry & 0xFFFF);
            var from = (ushort)(entry >> 16);

            if (to == 0 && from == 0)
            {
                continue;
            }

            list.Add(new Mapping(from, to));
        }

        return list;
    }

    private static string GetKeyDisplayName(ushort scanCode)
    {
        if (scanCode == 0)
        {
            return "Disabled";
        }

        var option = KeyOptions.FirstOrDefault(item => item.ScanCode == scanCode);
        return option is null ? $"Unknown (SC:0x{scanCode:X4})" : option.DisplayName;
    }

    private void DeleteMapping_Click(object sender, RoutedEventArgs e)
    {
        if (MappingList.SelectedItem is not MappingView view || view.Mapping is null)
        {
            UpdateStatus("삭제할 항목을 선택하세요.");
            return;
        }

        var confirm = MessageBox.Show("선택한 매핑을 삭제할까요?", "ReKey",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        if (!IsAdministrator())
        {
            UpdateStatus("관리자 권한이 필요합니다. 관리자 권한으로 다시 실행하세요.");
            return;
        }

        var mappings = LoadMappings().ToList();
        if (!mappings.Remove(view.Mapping))
        {
            UpdateStatus("선택한 항목을 찾을 수 없습니다.");
            return;
        }

        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Keyboard Layout", true);

        if (mappings.Count == 0)
        {
            key?.DeleteValue("Scancode Map", false);
        }
        else
        {
            var scancodeMap = ScancodeMapBuilder.Build(mappings);
            key?.SetValue("Scancode Map", scancodeMap, RegistryValueKind.Binary);
        }

        RefreshMappingList();
        UpdateStatus("선택한 매핑을 삭제했습니다. 재부팅 후 반영됩니다.");
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_fromKey is null || _toKey is null)
        {
            UpdateStatus("원본 키와 대상 키를 모두 선택하세요.");
            return;
        }

        if (!IsAdministrator())
        {
            UpdateStatus("관리자 권한이 필요합니다. 관리자 권한으로 다시 실행하세요.");
            return;
        }

        var scancodeMap = ScancodeMapBuilder.Build([new Mapping(_fromKey.ScanCode, _toKey.ScanCode)]);
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\Keyboard Layout", true);

        key?.SetValue("Scancode Map", scancodeMap, RegistryValueKind.Binary);

        RefreshMappingList();
        UpdateStatus($"적용 완료: {_fromKey.ToDisplayString()} -> {_toKey.ToDisplayString()} (재부팅 후 반영)");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAdministrator())
        {
            UpdateStatus("관리자 권한이 필요합니다. 관리자 권한으로 다시 실행하세요.");
            return;
        }

        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Keyboard Layout", true);

        if (key is null || key.GetValue("Scancode Map") is null)
        {
            UpdateStatus("Scancode Map이 존재하지 않습니다.");
            return;
        }

        key.DeleteValue("Scancode Map");
        RefreshMappingList();
        UpdateStatus("초기화 완료. 재부팅 후 반영됩니다.");
    }

    private void ToggleDiag_Click(object sender, RoutedEventArgs e)
    {
        if (_diagnosticEnabled)
        {
            StopDiagnostic();
            UpdateDiagText("진단: 꺼짐");
            DiagToggleButton.Content = "진단 시작";
        }
        else
        {
            StartDiagnostic();
            UpdateDiagText("진단: 대기 중...");
            DiagToggleButton.Content = "진단 중지";
        }
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void UpdateDiagText(string message)
    {
        DiagText.Text = message;
    }

    private static bool IsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void StartDiagnostic()
    {
        if (_diagnosticEnabled)
        {
            return;
        }

        _proc = HookCallback;
        _hookId = SetHook(_proc);
        _diagnosticEnabled = _hookId != IntPtr.Zero;
    }

    private void StopDiagnostic()
    {
        if (!_diagnosticEnabled)
        {
            return;
        }

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _proc = null;
        _diagnosticEnabled = false;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown))
        {
            var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
            var scanCode = data.scanCode;
            if ((data.flags & LlkfExtended) != 0)
            {
                scanCode |= 0xE000;
            }

            Dispatcher.Invoke(() =>
                UpdateDiagText($"진단: SC 0x{scanCode:X4} (flags:0x{data.flags:X2})"));
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private const uint MapVkToVscEx = 4;

    private static readonly IReadOnlyList<KeyOption> KeyOptions = new List<KeyOption>
    {
        new("PrintScreen (PRTSC)", 0x2C, 0xE037),
        new("Right Ctrl", 0xA3, 0xE01D),
        new("Left Ctrl", 0xA2, 0x001D),
        new("Right Alt (AltGr)", 0xA5, 0xE038),
        new("Left Alt", 0xA4, 0x0038),
        new("Left Shift", 0xA0, 0x002A),
        new("Right Shift", 0xA1, 0x0036),
        new("Enter", 0x0D, 0x001C),
        new("Numpad Enter", 0x0D, 0xE01C),
        new("Space", 0x20, 0x0039),
        new("Tab", 0x09, 0x000F),
        new("CapsLock", 0x14, 0x003A),
        new("Backspace", 0x08, 0x000E),
        new("Escape", 0x1B, 0x0001),
        new("Insert", 0x2D, 0xE052),
        new("Delete", 0x2E, 0xE053),
        new("Home", 0x24, 0xE047),
        new("End", 0x23, 0xE04F),
        new("Page Up", 0x21, 0xE049),
        new("Page Down", 0x22, 0xE051),
        new("Arrow Up", 0x26, 0xE048),
        new("Arrow Down", 0x28, 0xE050),
        new("Arrow Left", 0x25, 0xE04B),
        new("Arrow Right", 0x27, 0xE04D),
        new("Left Win", 0x5B, 0xE05B),
        new("Right Win", 0x5C, 0xE05C),
        new("Apps/Menu", 0x5D, 0xE05D),
        new("0", 0x30, 0x000B),
        new("1", 0x31, 0x0002),
        new("2", 0x32, 0x0003),
        new("3", 0x33, 0x0004),
        new("4", 0x34, 0x0005),
        new("5", 0x35, 0x0006),
        new("6", 0x36, 0x0007),
        new("7", 0x37, 0x0008),
        new("8", 0x38, 0x0009),
        new("9", 0x39, 0x000A),
        new("A", 0x41, 0x001E),
        new("B", 0x42, 0x0030),
        new("C", 0x43, 0x002E),
        new("D", 0x44, 0x0020),
        new("E", 0x45, 0x0012),
        new("F", 0x46, 0x0021),
        new("G", 0x47, 0x0022),
        new("H", 0x48, 0x0023),
        new("I", 0x49, 0x0017),
        new("J", 0x4A, 0x0024),
        new("K", 0x4B, 0x0025),
        new("L", 0x4C, 0x0026),
        new("M", 0x4D, 0x0032),
        new("N", 0x4E, 0x0031),
        new("O", 0x4F, 0x0018),
        new("P", 0x50, 0x0019),
        new("Q", 0x51, 0x0010),
        new("R", 0x52, 0x0013),
        new("S", 0x53, 0x001F),
        new("T", 0x54, 0x0014),
        new("U", 0x55, 0x0016),
        new("V", 0x56, 0x002F),
        new("W", 0x57, 0x0011),
        new("X", 0x58, 0x002D),
        new("Y", 0x59, 0x0015),
        new("Z", 0x5A, 0x002C)
    };

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint LlkfExtended = 0x01;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private static IntPtr SetHook(LowLevelKeyboardProc proc)
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        return SetWindowsHookEx(WhKeyboardLl, proc, GetModuleHandle(curModule?.ModuleName), 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private sealed record KeyInfo(string Name, int VirtualKey, ushort ScanCode)
    {
        public string ToDisplayString() => $"{Name} (VK:{VirtualKey}, SC:0x{ScanCode:X4})";
    }

    private sealed record KeyOption(string DisplayName, int VirtualKey, ushort ScanCode);

    private sealed record Mapping(ushort FromScanCode, ushort ToScanCode);

    private sealed record MappingView(Mapping? Mapping, string MappingDisplay);

    private static class ScancodeMapBuilder
    {
        public static byte[] Build(IReadOnlyList<Mapping> mappings)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(0u);
            writer.Write(0u);
            writer.Write((uint)(mappings.Count + 1));

            foreach (var mapping in mappings)
            {
                var entry = (uint)(mapping.ToScanCode | (mapping.FromScanCode << 16));
                writer.Write(entry);
            }

            writer.Write(0u);
            return stream.ToArray();
        }
    }
}
