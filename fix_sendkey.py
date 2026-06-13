import sys

def modify_program():
    cs_path = 'Program.cs'
    with open(cs_path, 'r', encoding='utf-8') as f:
        content = f.read()

    old_sendkey = """        private void SendKeyToWoW(int vKey)
        {
            IntPtr wowWindow = FindWindow(null, "World of Warcraft");
            if (wowWindow != IntPtr.Zero)
            {
                PostMessage(wowWindow, WM_KEYDOWN, (IntPtr)vKey, IntPtr.Zero);
                PostMessage(wowWindow, WM_KEYUP, (IntPtr)vKey, IntPtr.Zero);
            }
        }"""

    new_sendkey = """        private void SendKeyToWoW(int vKey)
        {
            try 
            {
                System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(char1.Pid);
                if (p != null)
                {
                    IntPtr wowWindow = p.MainWindowHandle;
                    if (wowWindow != IntPtr.Zero)
                    {
                        PostMessage(wowWindow, WM_KEYDOWN, (IntPtr)vKey, IntPtr.Zero);
                        PostMessage(wowWindow, WM_KEYUP, (IntPtr)vKey, IntPtr.Zero);
                    }
                }
            } 
            catch { }
        }"""

    if old_sendkey in content:
        content = content.replace(old_sendkey, new_sendkey)
        with open(cs_path, 'w', encoding='utf-8') as f:
            f.write(content)
        print("Updated SendKeyToWoW successfully.")
    else:
        print("Could not find SendKeyToWoW in Program.cs.")

modify_program()
