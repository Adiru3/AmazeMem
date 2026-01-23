# AmazeMem

A high-performance, lightweight system utility designed for competitive gamers and power users to minimize input lag and eliminate system stutters by managing Windows memory lists at a kernel level.

<img width="424" height="465" alt="image" src="https://github.com/user-attachments/assets/6f06878b-d722-438d-8ffc-3c698582529d" />

<img width="839" height="75" alt="image" src="https://github.com/user-attachments/assets/6ff436e2-01b8-4ef6-a5ba-073261e3bf59" />

## 🚀 Key Features (Based on Source Code)

* **Kernel-Level Memory Purge**: Directly interfaces with `ntdll.dll` using `NtSetSystemInformation` to clear:
    * **Standby List**: Removes cached files that cause micro-stutters.
    * **Modified Page List**: Flushes pages waiting to be written to disk.
    * **Priority 0 Standby List**: Cleans the lowest priority memory pages.
* **SYSTEM-Level Execution**: Automatically deploys a Windows Task Scheduler task (`AmazingMemCleaner`) to run with **SYSTEM (S-1-5-18)** privileges. This bypasses typical "Access Denied" errors when clearing system-protected memory.
* **Working Set Reduction**: Iterates through all active processes and uses `psapi.dll` (`EmptyWorkingSet`) to reclaim physical RAM occupied by non-critical applications.
* **Smart Automation & Logging**:
    * **RAM Threshold Trigger**: Monitors RAM usage via `PerformanceCounter` and triggers cleaning if usage exceeds your set limit (e.g., 90%).
    * **Rolling Logs**: Maintains a real-time `cleaner.log` with a strict **50-line limit** to prevent disk bloating while keeping you informed.
* **Advanced Privilege Escalation**: Specifically activates `SeProfileSingleProcessPrivilege`, `SeIncreaseQuotaPrivilege`, and `SeDebugPrivilege` within the process token for deep system access.
* **Persistence & Stealth**:
    * **Registry-Based Settings**: Saves all configurations in `HKEY_LOCAL_MACHINE\SOFTWARE\AmazeMem`.
    * **Auto-Installation**: Copies itself to `C:\Program Files\AmazeMem` and sets up auto-start with highest privileges.
    * **Tray Integration**: Runs in the background with a system tray icon.

## 🛠 Cleaning Modes Detailed

| Mode | Technical Implementation |
| :--- | :--- |
| **Working Sets** | Flushes the private working set of every accessible process. |
| **Standby List** | Uses `MemoryPurgeStandbyList` (Command 4) via `NtSetSystemInformation`. |
| **Modified List** | Uses `MemoryFlushModifiedList` (Command 3) via `NtSetSystemInformation`. |
| **Priority 0** | Uses Command 3/5 logic to free up high-speed RAM. |

## 📦 Technical Info
* **Language**: C# 
* **Framework**: .NET Framework 4.8 (Legacy `csc.exe` compatible)
* **Privileges Required**: Administrator (for setup) / SYSTEM (for cleaning)

## 🎯 Who needs AmazeMem? (Checklist)

If at least one of these points describes your situation, you will notice the difference immediately:

* **Users with 8GB or 16GB RAM**: If your memory is constantly bloated by browsers, Discord, and system cache, your games lack the "clean" space required to operate. This often forces Windows into **Disk Swapping** (Pagefile usage), which is significantly slower than physical RAM.
* **Competitive FPS Players (CS2, Valorant, Quake, Apex)**: For those who demand the absolute minimum **Input Lag**. Kernel-level Standby List purging eliminates the delay between your mouse movement and the on-screen reaction.
* **Victims of "Micro-stutters"**: If your FPS counter is high but the image feels "choppy" or hitches during fast 180-degree turns, you are likely experiencing a conflict between the game engine and the Windows cache.
* **Low-to-Mid End PC Owners**: When every megabyte counts, the automatic reduction of **Working Sets** for background apps frees up vital physical resources for your primary game process.
* **Users who rarely Reboot**: Over time, Windows accumulates "junk" in the **Modified Page List** that isn't cleared automatically. AmazeMem forces this cleanup, restoring system snappiness without a restart.
* **Streamers & Multitaskers**: If you play with OBS, browsers with 20+ tabs, and music apps open, AmazeMem keeps their "appetite" in check. It prevents background software from "eating up" the high-priority memory your game needs.
* **Optimization Perfectionists**: If you have already tuned your Windows, BIOS, and Network, but the system still feels "floaty," AmazeMem solves the final piece of the puzzle through **SYSTEM-level** memory management.

---
*Optimized for minimal Latency. Created by amazingb01 (Adiru).*

## 🔗 Connect with me

[![YouTube](https://img.shields.io/badge/YouTube-@adiruaim-FF0000?style=for-the-badge&logo=youtube)](https://www.youtube.com/@adiruaim)
[![TikTok](https://img.shields.io/badge/TikTok-@adiruhs-000000?style=for-the-badge&logo=tiktok)](https://www.tiktok.com/@adiruhs)
[![Donatello](https://img.shields.io/badge/Support-Donatello-orange?style=for-the-badge)](https://donatello.to/Adiru3)
