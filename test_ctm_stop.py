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

# Find a nearby player or unit to target
cur_obj = read_uint32(obj_mgr + 0xAC)
visited = set()
target_guid = 0
target_x, target_y, target_z = 0.0, 0.0, 0.0

while cur_obj != 0 and cur_obj > 0x10000 and cur_obj not in visited:
    visited.add(cur_obj)
    guid = read_uint64(cur_obj + 0x30)
    obj_type = read_uint32(cur_obj + 0x14)
    
    if guid != local_guid and (obj_type == 3 or obj_type == 4):
        target_guid = guid
        target_x = read_float(cur_obj + 0x798)
        target_y = read_float(cur_obj + 0x79C)
        target_z = read_float(cur_obj + 0x7A0)
        print(f"Found unit: GUID={guid:016X}, Pos=({target_x:.2f}, {target_y:.2f}, {target_z:.2f})")
        break
    cur_obj = read_uint32(cur_obj + 0x3C)

if target_guid == 0:
    print("No unit found.")
    CloseHandle(h_process)
    sys.exit(0)

# CTM Base Offset
CTM_BASE = 0x00CA11D8

print("Triggering CTM Interact (Action 4)...")
write_float(CTM_BASE + 0x8C, target_x)
write_float(CTM_BASE + 0x90, target_y)
write_float(CTM_BASE + 0x94, target_z)
write_uint64(CTM_BASE + 0x20, target_guid)
write_uint32(CTM_BASE + 0x1C, 4) # 4 = Interact

# Sleep for a tiny amount of time (20 milliseconds)
time.sleep(0.02)

# Instantly clear CTM action
print("Instantly clearing CTM action...")
write_uint32(CTM_BASE + 0x1C, 0) # 0 = None

time.sleep(0.2)
target_addr = 0x00BD07B0
print(f"Target GUID in memory after short CTM: {read_uint64(target_addr):016X}")

CloseHandle(h_process)
