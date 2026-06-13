import ctypes
from ctypes import wintypes
import struct
import sys
import time

PROCESS_QUERY_INFORMATION = 0x0400
PROCESS_VM_READ = 0x0010
PROCESS_VM_WRITE = 0x0020
PROCESS_VM_OPERATION = 0x0008

OpenProcess = ctypes.windll.kernel32.OpenProcess
OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
OpenProcess.restype = wintypes.HANDLE

ReadProcessMemory = ctypes.windll.kernel32.ReadProcessMemory
ReadProcessMemory.argtypes = [wintypes.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
ReadProcessMemory.restype = wintypes.BOOL

WriteProcessMemory = ctypes.windll.kernel32.WriteProcessMemory
WriteProcessMemory.argtypes = [wintypes.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
WriteProcessMemory.restype = wintypes.BOOL

CloseHandle = ctypes.windll.kernel32.CloseHandle

def get_wow_pids():
    import subprocess
    import re
    output = subprocess.check_output("tasklist", shell=True).decode('utf-8', errors='ignore')
    pids = []
    for line in output.splitlines():
        if "wow" in line.lower():
            parts = re.split(r'\s+', line.strip())
            for part in parts[1:]:
                if part.isdigit():
                    pids.append(int(part))
                    break
    return pids

pids = get_wow_pids()
if not pids:
    print("WoW not running")
    sys.exit(1)

pid = pids[0]
print(f"Using WoW process PID: {pid}")

h_process = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, False, pid)
if not h_process:
    print("Failed to open process")
    sys.exit(1)

def read_uint32(addr):
    buf = ctypes.create_string_buffer(4)
    read = ctypes.c_size_t(0)
    if ReadProcessMemory(h_process, ctypes.c_void_p(addr), buf, 4, ctypes.byref(read)) and read.value == 4:
        return struct.unpack("<I", buf.raw)[0]
    return 0

def read_uint64(addr):
    buf = ctypes.create_string_buffer(8)
    read = ctypes.c_size_t(0)
    if ReadProcessMemory(h_process, ctypes.c_void_p(addr), buf, 8, ctypes.byref(read)) and read.value == 8:
        return struct.unpack("<Q", buf.raw)[0]
    return 0

def read_float(addr):
    buf = ctypes.create_string_buffer(4)
    read = ctypes.c_size_t(0)
    if ReadProcessMemory(h_process, ctypes.c_void_p(addr), buf, 4, ctypes.byref(read)) and read.value == 4:
        return struct.unpack("<f", buf.raw)[0]
    return 0.0

def write_uint32(addr, val):
    buf = struct.pack("<I", val)
    written = ctypes.c_size_t(0)
    return WriteProcessMemory(h_process, ctypes.c_void_p(addr), buf, 4, ctypes.byref(written))

def write_uint64(addr, val):
    buf = struct.pack("<Q", val)
    written = ctypes.c_size_t(0)
    return WriteProcessMemory(h_process, ctypes.c_void_p(addr), buf, 8, ctypes.byref(written))

def write_float(addr, val):
    buf = struct.pack("<f", val)
    written = ctypes.c_size_t(0)
    return WriteProcessMemory(h_process, ctypes.c_void_p(addr), buf, 4, ctypes.byref(written))

local_guid = read_uint64(0x00CA1238)
client_conn = read_uint32(0x00C79CE0)
obj_mgr = read_uint32(client_conn + 0x2ED0)

# Get local player object coordinates
local_x, local_y, local_z = 0.0, 0.0, 0.0
cur_obj = read_uint32(obj_mgr + 0xAC)
visited = set()
while cur_obj != 0 and cur_obj > 0x10000 and cur_obj not in visited:
    visited.add(cur_obj)
    guid = read_uint64(cur_obj + 0x30)
    if guid == local_guid:
        local_x = read_float(cur_obj + 0x798)
        local_y = read_float(cur_obj + 0x79C)
        local_z = read_float(cur_obj + 0x7A0)
        print(f"Local Player: Pos=({local_x:.2f}, {local_y:.2f}, {local_z:.2f})")
        break
    cur_obj = read_uint32(cur_obj + 0x3C)

# Find a nearby player or unit to target
cur_obj = read_uint32(obj_mgr + 0xAC)
visited = set()
target_guid = 0

while cur_obj != 0 and cur_obj > 0x10000 and cur_obj not in visited:
    visited.add(cur_obj)
    guid = read_uint64(cur_obj + 0x30)
    obj_type = read_uint32(cur_obj + 0x14)
    
    if guid != local_guid and (obj_type == 3 or obj_type == 4):
        target_guid = guid
        print(f"Found unit to target: GUID={guid:016X}, Type={obj_type}")
        break
    cur_obj = read_uint32(cur_obj + 0x3C)

if target_guid == 0:
    print("No unit found to target.")
    CloseHandle(h_process)
    sys.exit(0)

# CTM Base Offset
CTM_BASE = 0x00CA11D8

print("Writing CTM Interact (Action 4) on GUID at local player's coordinates...")
# Set destination coordinates to local player's position
write_float(CTM_BASE + 0x8C, local_x)
write_float(CTM_BASE + 0x90, local_y)
write_float(CTM_BASE + 0x94, local_z)
# Set target GUID and action
write_uint64(CTM_BASE + 0x20, target_guid)
write_uint32(CTM_BASE + 0x1C, 4) # 4 = Interact

time.sleep(0.1)

# Check target in memory
target_addr = 0x00BD07B0
print(f"Target GUID in memory after CTM: {read_uint64(target_addr):016X}")

# Clear CTM Action to stop any potential action
write_uint32(CTM_BASE + 0x1C, 0)

CloseHandle(h_process)
