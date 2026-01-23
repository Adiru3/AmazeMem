# AmazeMem

A high-performance, lightweight system utility designed for competitive gamers and power users to minimize input lag and eliminate system stutters by managing Windows memory lists at a kernel level.

<img width="424" height="465" alt="image" src="https://github.com/user-attachments/assets/6f06878b-d722-438d-8ffc-3c698582529d" />

<img width="839" height="75" alt="image" src="https://github.com/user-attachments/assets/6ff436e2-01b8-4ef6-a5ba-073261e3bf59" />

Created by **amazingb01 (Adiru)**.

## 🚀 Key Features (Based on Source Code)

* **Kernel-Level Memory Purge**: Directly interfaces with `ntdll.dll` using `NtSetSystemInformation` to clear:
    * **Standby List**: Removes cached files that cause micro-stutters.
    * **Modified Page List**: Flushes pages waiting to be written to disk.
    * **Priority 0 Standby List**: Cleans the lowest priority memory pages.
* **SYSTEM-Level Execution**: Automatically deploys a Windows Task Scheduler task (`AmazingMemCleaner`) to run with **SYSTEM (S-1-5-18)** privileges. This bypasses typical "Access Denied" errors (0xC0000061) when clearing system-protected memory.
* **Working Set Reduction**: Iterates through all active processes and uses `psapi.dll` (`EmptyWorkingSet`) to reclaim physical RAM occupied by non-critical applications.
* **Smart Automation Logic**:
    * **RAM Threshold Trigger**: Monitors RAM usage via `PerformanceCounter` and triggers cleaning if usage exceeds your set limit (e.g., 90%).
    * **Dynamic Interval**: Supports periodic cleaning based on a user-defined timer (in minutes).
* **Advanced Privilege Escalation**: Specifically activates `SeProfileSingleProcessPrivilege`, `SeIncreaseQuotaPrivilege`, and `SeDebugPrivilege` within the process token for deep system access.
* **Persistence & Stealth**:
    * **Registry-Based Settings**: Saves all configurations (threshold, interval, enabled modes, language) in `HKEY_LOCAL_MACHINE\SOFTWARE\AmazeMem`.
    * **Auto-Installation**: Copies itself to `C:\Program Files\AmazeMem` and sets up auto-start with highest privileges.
    * **Tray Integration**: Runs in the background with a system tray icon and double-click to restore.

## 🛠 Cleaning Modes Detailed

| Mode | Technical Implementation |
| :--- | :--- |
| **Working Sets** | Flushes the private working set of every accessible process. |
| **Standby List** | Uses `MemoryPurgeStandbyList` (Command 4) via `NtSetSystemInformation`. |
| **Modified List** | Uses `MemoryFlushModifiedList` (Command 3) via `NtSetSystemInformation`. |
| **Priority 0** | Uses `MemoryPurgeLowPriorityStandbyList` (Command 5) to free up high-speed RAM. |

---
*Optimized for minimal Latency. Compiled with .NET Framework 4.8.*

## 🔗 Connect with me

[![YouTube](https://img.shields.io/badge/YouTube-@adiruaim-FF0000?style=for-the-badge&logo=youtube)](https://www.youtube.com/@adiruaim)
[![TikTok](https://img.shields.io/badge/TikTok-@adiruhs-000000?style=for-the-badge&logo=tiktok)](https://www.tiktok.com/@adiruhs)
[![Donatello](https://img.shields.io/badge/Support-Donatello-orange?style=for-the-badge)](https://donatello.to/Adiru3)

---
