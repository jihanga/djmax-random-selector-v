using System;
using System.Runtime.InteropServices;
using Dmrsv.RandomSelector.Enums;

namespace Dmrsv.RandomSelector
{
    public class Locator
    {
        public int InputInterval { get; set; } = 30;
        public bool LocatesStyle { get; set; } = true;
        public bool CanLocate { get; set; } = true;
        public bool PressesStart { get; set; } = false;

        private List<LocationInfo?> _locations;

        private readonly Dictionary<string, ushort> _keyMap = new()
        {
            ["1"] = 0x02, ["2"] = 0x03, ["3"] = 0x04, ["4"] = 0x05, ["5"] = 0x06,
            ["6"] = 0x07, ["7"] = 0x08, ["8"] = 0x09, ["9"] = 0x0A, ["0"] = 0x0B,
            ["q"] = 0x10, ["w"] = 0x11, ["e"] = 0x12, ["r"] = 0x13, ["t"] = 0x14,
            ["y"] = 0x15, ["u"] = 0x16, ["i"] = 0x17, ["o"] = 0x18, ["p"] = 0x19,
            ["a"] = 0x1E, ["s"] = 0x1F, ["d"] = 0x20, ["f"] = 0x21, ["g"] = 0x22,
            ["h"] = 0x23, ["j"] = 0x24, ["k"] = 0x25, ["l"] = 0x26, [";"] = 0x27,
            ["z"] = 0x2C, ["x"] = 0x2D, ["c"] = 0x2E, ["v"] = 0x2F, ["b"] = 0x30,
            ["n"] = 0x31, ["m"] = 0x32,
            ["f5"] = 0x3F,
            ["shiftleft"] = 0x2A, ["shiftright"] = 0x36,
            ["pageup"] = 0xC9 + 1024, ["pagedown"] = 0xD1 + 1024,
        };

        public Locator()
        {
            _locations = new List<LocationInfo?>();
            _keyMap.Add("left", (ushort)MapVirtualKey(0x25, 0));
            _keyMap.Add("up", (ushort)MapVirtualKey(0x26, 0));
            _keyMap.Add("right", (ushort)MapVirtualKey(0x27, 0));
            _keyMap.Add("down", (ushort)MapVirtualKey(0x28, 0));
        }

        public void MakeLocations(IEnumerable<Track> trackList)
        {
            var getGroup = (Track t) =>
            {
                return TitleComparer.StartsWithAlphabet(t.Title) ? char.ToLower(t.Title[0]) : '#';
            };
            var groupByInitial = trackList.Where(t => t.IsPlayable)
                                          .OrderBy(t => t.Title, new TitleComparer())
                                          .ThenByDescending(t => SortRules.Current.GetTiebreak(t.Id))
                                          .GroupBy(t => getGroup(t))
                                          .ToDictionary(g => g.Key, g => g.ToList());
            var getIndex = (Track t) =>
            {
                char initial = char.ToLower(getGroup(t));
                int index = groupByInitial[initial].IndexOf(t);
                int count = groupByInitial[initial].Count();
                return index <= (count - 1) / 2 || "wxyzWXYZ".Contains(initial) ? index : index - count;
            };

            _locations = trackList.Select(track => 
            {
                if (!track.IsPlayable)
                {
                    return null;
                }
                return new LocationInfo()
                {
                    TrackId = track.Id,
                    Group = getGroup(track),
                    Index = getIndex(track),
                    DifficultyOrder = track.Patterns
                                           .GroupBy(p => p.Button)
                                           .SelectMany(g => g.Select((p, order) => new { p.Style, order }))
                                           .ToDictionary(o => o.Style, o => o.order)
                };
            }).ToList();
        }
        
        public void Locate(Pattern pattern)
        {
            if (!CanLocate)
            {
                return;
            }
            LocationInfo? loc = _locations[pattern.TrackId];
            if (loc is null)
            {
                return;
            }
            ResetMusicCursor();

            // input initial letter of title
            char group = loc.Group;
            if (loc.Index < 0)
            {
                if (group == '#')
                {
                    group = 'a';
                }
                else if (group == 'z')
                {
                    group = '#';
                }
                else
                {
                    group = (char)(group + 1);
                }
            }
            if (group != '#')
            {
                Input(_keyMap[group.ToString()]);
            }

            // locate to track
            string arrow = loc.Index < 0 ? "up" : "down";
            int distance = Math.Abs(loc.Index);
            RepeatInputs(distance, _keyMap[arrow], true);

            int difficultyOrder = loc.DifficultyOrder[pattern.Style];
            if (LocatesStyle)
            {
                SelectButton(pattern.Button.AsString()[0]);
                RepeatInputs(difficultyOrder, _keyMap["right"], true);
            }
            if (PressesStart)
            {
                int startDelay = 800 - InputInterval * (difficultyOrder + 1);
                startDelay = startDelay < 0 ? 0 : startDelay;
                Thread.Sleep(startDelay);
                Input(_keyMap["f5"]);
            }
        }

        private void KeyDown(ushort key, bool isArrowKey = false)
        {
            SendKey(key, isArrowKey, false);
            Thread.Sleep(InputInterval);
        }

        private void KeyUp(ushort key, bool isArrowKey = false)
        {
            SendKey(key, isArrowKey, true);
            Thread.Sleep(InputInterval);
        }

        private void Input(ushort key, bool isArrowKey = false)
        {
            KeyDown(key, isArrowKey);
            KeyUp(key, isArrowKey);
        }

        private void RepeatInputs(int number, ushort key, bool isArrowKey = false)
        {
            for (int i = 0; i < number; i++)
            {
                Input(key, isArrowKey);
            }
        }

        private void ResetMusicCursor()
        {
            KeyDown(_keyMap["shiftright"]);
            KeyDown(_keyMap["shiftleft"]);
            KeyUp(_keyMap["shiftright"]);
            KeyUp(_keyMap["shiftleft"]);
        }

        private void SelectButton(char button)
        {
            ushort ctrl = 0x1D;
            ushort scancode = button switch
            {
                '4' => 0x4B,
                '5' => 0x4C,
                '6' => 0x4D,
                '8' => 0x48,
                _ => throw new NotImplementedException(),
            };
            KeyDown(ctrl);
            KeyDown(scancode);
            KeyUp(scancode);
            KeyUp(ctrl);
        }

        private void SendKey(ushort scanCode, bool isArrowKey, bool isKeyUp)
        {
            // Original code from https://github.com/learncodebygaming/pydirectinput
            // Copyright(c) 2020 Ben Johnson
            const uint FlagExtendedKey = 0x0001;    // KEYEVENTF_EXTENDEDKEY
            const uint FlagKeyUp = 0x0002;          // KEYEVENTF_KEYUP
            const uint FlagScanCode = 0x0008;       // KEYEVENTF_SCANCODE
            uint flags = FlagScanCode | (isKeyUp ? FlagKeyUp : 0);

            INPUT key = MakeInput(scanCode, flags | (isArrowKey ? FlagExtendedKey : 0));
            // NumLock이 켜져 있으면 방향키를 누르기 전과 뗀 후에 0xE0 스캔코드를 함께 보낸다.
            if (isArrowKey && GetKeyState(0x90) != 0)
            {
                INPUT prefix = MakeInput(0xE0, flags);
                SendOne(isKeyUp ? key : prefix);
                SendOne(isKeyUp ? prefix : key);
                return;
            }
            SendOne(key);
        }

        private static INPUT MakeInput(ushort scanCode, uint flags)
        {
            INPUT input = new INPUT { Type = 1 };
            input.Data.Keyboard = new KEYBDINPUT { Scan = scanCode, Flags = flags };
            return input;
        }

        private static void SendOne(INPUT input)
        {
            SendInput(1, new INPUT[] { input }, Marshal.SizeOf(typeof(INPUT)));
        }

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(int wCode, int wMapType);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int sizeOfInputStructure);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint Msg;
            public ushort ParamL;
            public ushort ParamH;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct MOUSEKEYBDHARDWAREINPUT
        {
            [FieldOffset(0)]
            public HARDWAREINPUT Hardware;
            [FieldOffset(0)]
            public KEYBDINPUT Keyboard;
            [FieldOffset(0)]
            public MOUSEINPUT Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public MOUSEKEYBDHARDWAREINPUT Data;
        }
    }
}
