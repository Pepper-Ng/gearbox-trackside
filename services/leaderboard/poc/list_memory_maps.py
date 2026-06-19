from __future__ import annotations

import argparse
import ctypes
import json
import os
from ctypes import wintypes
from dataclasses import dataclass, asdict


RF2_MAP_BASE_NAMES = [
    "$rFactor2SMMP_Telemetry$",
    "$rFactor2SMMP_Scoring$",
    "$rFactor2SMMP_Rules$",
    "$rFactor2SMMP_MultiRules$",
    "$rFactor2SMMP_ForceFeedback$",
    "$rFactor2SMMP_Graphics$",
    "$rFactor2SMMP_PitInfo$",
    "$rFactor2SMMP_Weather$",
    "$rFactor2SMMP_Extended$",
    "$rFactor2SMMP_HWControl$",
]

FILE_MAP_READ = 0x0004
DIRECTORY_QUERY = 0x0001
OBJ_CASE_INSENSITIVE = 0x00000040
STATUS_NO_MORE_ENTRIES = 0x8000001A


@dataclass
class ProbeResult:
    name: str
    opened: bool
    error: str | None = None


@dataclass
class ObjectEntry:
    directory: str
    name: str
    type_name: str


class UNICODE_STRING(ctypes.Structure):
    _fields_ = [
        ("Length", wintypes.USHORT),
        ("MaximumLength", wintypes.USHORT),
        ("Buffer", wintypes.LPWSTR),
    ]


class OBJECT_ATTRIBUTES(ctypes.Structure):
    _fields_ = [
        ("Length", wintypes.ULONG),
        ("RootDirectory", wintypes.HANDLE),
        ("ObjectName", ctypes.POINTER(UNICODE_STRING)),
        ("Attributes", wintypes.ULONG),
        ("SecurityDescriptor", wintypes.LPVOID),
        ("SecurityQualityOfService", wintypes.LPVOID),
    ]


class OBJECT_DIRECTORY_INFORMATION(ctypes.Structure):
    _fields_ = [
        ("Name", UNICODE_STRING),
        ("TypeName", UNICODE_STRING),
    ]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="List visible Windows named memory maps and probe rF2 shared-memory map names."
    )
    parser.add_argument(
        "--pid",
        type=int,
        help="Dedicated.exe process ID. Adds PID-suffixed rF2 map names to the probe list.",
    )
    parser.add_argument(
        "--filter",
        default="rFactor2SMMP",
        help="Only print enumerated Section objects whose name contains this text. Use --all to disable filtering.",
    )
    parser.add_argument(
        "--all",
        action="store_true",
        help="Print all visible named Section objects in inspected object directories.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Output JSON instead of text.",
    )
    return parser.parse_args()


def main() -> None:
    if os.name != "nt":
        raise SystemExit("This diagnostic only works on Windows.")

    args = parse_args()
    configure_win32_api()
    session_id = get_current_session_id()
    target_session_id = get_process_session_id(args.pid) if args.pid is not None else None
    directories = visible_object_directories(session_id, target_session_id)
    probes = probe_rf2_map_names(args.pid)
    entries = enumerate_named_sections(directories)
    filtered_entries = filter_entries(entries, args.filter, args.all)

    if args.json:
        print(
            json.dumps(
                {
                    "session_id": session_id,
                    "target_pid": args.pid,
                    "target_session_id": target_session_id,
                    "directories": directories,
                    "probes": [asdict(result) for result in probes],
                    "sections": [asdict(entry) for entry in filtered_entries],
                },
                indent=2,
            )
        )
        return

    print(f"Current Windows session ID: {session_id}")
    if args.pid is not None:
        print(f"Target PID: {args.pid}")
        print(f"Target PID Windows session ID: {target_session_id if target_session_id is not None else '<unavailable>'}")
    print("Inspected object directories:")
    for directory in directories:
        print(f"  {directory}")
    print()
    print("rF2 map open probes:")
    for result in probes:
        status = "OPEN" if result.opened else "missing"
        detail = f" ({result.error})" if result.error else ""
        print(f"  {status:7} {result.name}{detail}")

    print()
    print("Visible named Section objects:")
    if not filtered_entries:
        print("  <none matched>")
    for entry in filtered_entries:
        print(f"  {entry.directory}\\{entry.name} [{entry.type_name}]")

    print()
    print("Note: this enumerates named Section objects visible to the current user/session.")
    print("Private unnamed mappings, inaccessible namespaces, or maps in other sessions may not appear.")


def configure_win32_api() -> None:
    kernel32.OpenFileMappingW.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.LPCWSTR]
    kernel32.OpenFileMappingW.restype = wintypes.HANDLE
    kernel32.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel32.CloseHandle.restype = wintypes.BOOL
    kernel32.GetCurrentProcessId.argtypes = []
    kernel32.GetCurrentProcessId.restype = wintypes.DWORD
    kernel32.ProcessIdToSessionId.argtypes = [wintypes.DWORD, ctypes.POINTER(wintypes.DWORD)]
    kernel32.ProcessIdToSessionId.restype = wintypes.BOOL

    ntdll.NtOpenDirectoryObject.argtypes = [
        ctypes.POINTER(wintypes.HANDLE),
        wintypes.DWORD,
        ctypes.POINTER(OBJECT_ATTRIBUTES),
    ]
    ntdll.NtOpenDirectoryObject.restype = wintypes.LONG
    ntdll.NtQueryDirectoryObject.argtypes = [
        wintypes.HANDLE,
        wintypes.LPVOID,
        wintypes.ULONG,
        wintypes.BOOLEAN,
        wintypes.BOOLEAN,
        ctypes.POINTER(wintypes.ULONG),
        ctypes.POINTER(wintypes.ULONG),
    ]
    ntdll.NtQueryDirectoryObject.restype = wintypes.LONG


def get_current_session_id() -> int:
    session_id = wintypes.DWORD(0)
    if not kernel32.ProcessIdToSessionId(kernel32.GetCurrentProcessId(), ctypes.byref(session_id)):
        return -1
    return int(session_id.value)


def get_process_session_id(pid: int | None) -> int | None:
    if pid is None:
        return None
    session_id = wintypes.DWORD(0)
    if not kernel32.ProcessIdToSessionId(pid, ctypes.byref(session_id)):
        return None
    return int(session_id.value)


def visible_object_directories(current_session_id: int, target_session_id: int | None) -> list[str]:
    directories = [r"\BaseNamedObjects"]
    for session_id in dedupe_ints([current_session_id, target_session_id, 0]):
        if session_id is not None and session_id >= 0:
            directories.append(rf"\Sessions\{session_id}\BaseNamedObjects")
    return dedupe(directories)


def probe_rf2_map_names(pid: int | None) -> list[ProbeResult]:
    names: list[str] = []
    for base_name in RF2_MAP_BASE_NAMES:
        if pid is not None:
            names.append(f"{base_name}{pid}")
            names.append(f"Global\\{base_name}{pid}")
        names.append(base_name)

    results: list[ProbeResult] = []
    for name in dedupe(names):
        handle = kernel32.OpenFileMappingW(FILE_MAP_READ, False, name)
        if handle:
            kernel32.CloseHandle(handle)
            results.append(ProbeResult(name=name, opened=True))
        else:
            results.append(ProbeResult(name=name, opened=False, error=last_win32_error()))
    return results


def enumerate_named_sections(directories: list[str]) -> list[ObjectEntry]:
    entries: list[ObjectEntry] = []
    for directory in directories:
        try:
            entries.extend(enumerate_directory(directory))
        except OSError as exc:
            entries.append(ObjectEntry(directory=directory, name=f"<error: {exc}>", type_name="Error"))
    return entries


def enumerate_directory(directory: str) -> list[ObjectEntry]:
    handle = open_object_directory(directory)
    try:
        context = wintypes.ULONG(0)
        restart_scan = True
        found: list[ObjectEntry] = []
        while True:
            buffer = ctypes.create_string_buffer(64 * 1024)
            returned_length = wintypes.ULONG(0)
            status = ntdll.NtQueryDirectoryObject(
                handle,
                buffer,
                ctypes.sizeof(buffer),
                True,
                restart_scan,
                ctypes.byref(context),
                ctypes.byref(returned_length),
            )
            restart_scan = False
            unsigned_status = ctypes.c_ulong(status).value
            if unsigned_status == STATUS_NO_MORE_ENTRIES:
                break
            if status < 0:
                raise OSError(f"NtQueryDirectoryObject({directory}) failed: 0x{unsigned_status:08X}")

            item = ctypes.cast(buffer, ctypes.POINTER(OBJECT_DIRECTORY_INFORMATION)).contents
            name = unicode_string_to_text(item.Name)
            type_name = unicode_string_to_text(item.TypeName)
            if name:
                found.append(ObjectEntry(directory=directory, name=name, type_name=type_name))
        return found
    finally:
        kernel32.CloseHandle(handle)


def open_object_directory(directory: str) -> wintypes.HANDLE:
    name, backing_buffer = make_unicode_string(directory)
    attributes = OBJECT_ATTRIBUTES(
        Length=ctypes.sizeof(OBJECT_ATTRIBUTES),
        RootDirectory=None,
        ObjectName=ctypes.pointer(name),
        Attributes=OBJ_CASE_INSENSITIVE,
        SecurityDescriptor=None,
        SecurityQualityOfService=None,
    )
    handle = wintypes.HANDLE()
    status = ntdll.NtOpenDirectoryObject(
        ctypes.byref(handle),
        DIRECTORY_QUERY,
        ctypes.byref(attributes),
    )
    _ = backing_buffer
    if status < 0:
        raise OSError(f"NtOpenDirectoryObject({directory}) failed: 0x{ctypes.c_ulong(status).value:08X}")
    return handle


def make_unicode_string(value: str) -> tuple[UNICODE_STRING, ctypes.Array[wintypes.WCHAR]]:
    buffer = ctypes.create_unicode_buffer(value)
    string = UNICODE_STRING(
        Length=len(value) * ctypes.sizeof(wintypes.WCHAR),
        MaximumLength=(len(value) + 1) * ctypes.sizeof(wintypes.WCHAR),
        Buffer=ctypes.cast(buffer, wintypes.LPWSTR),
    )
    return string, buffer


def unicode_string_to_text(value: UNICODE_STRING) -> str:
    if not value.Buffer or value.Length == 0:
        return ""
    return ctypes.wstring_at(value.Buffer, value.Length // ctypes.sizeof(wintypes.WCHAR))


def filter_entries(entries: list[ObjectEntry], filter_text: str, include_all: bool) -> list[ObjectEntry]:
    if include_all:
        return [entry for entry in entries if entry.type_name == "Section"]
    lowered = filter_text.lower()
    return [
        entry
        for entry in entries
        if entry.type_name == "Section" and lowered in entry.name.lower()
    ]


def dedupe(values: list[str]) -> list[str]:
    result: list[str] = []
    for value in values:
        if value not in result:
            result.append(value)
    return result


def dedupe_ints(values: list[int | None]) -> list[int | None]:
    result: list[int | None] = []
    for value in values:
        if value not in result:
            result.append(value)
    return result


def last_win32_error() -> str:
    error_code = ctypes.get_last_error()
    return f"Windows error {error_code}" if error_code else "unknown error"


kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
ntdll = ctypes.WinDLL("ntdll", use_last_error=True)


if __name__ == "__main__":
    main()