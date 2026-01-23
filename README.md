# AmazeMem

A high-performance, lightweight system utility designed for competitive gamers and power users to minimize input lag and eliminate system stutters by managing Windows memory lists at a kernel level.

<img width="424" height="465" alt="image" src="https://github.com/user-attachments/assets/6f06878b-d722-438d-8ffc-3c698582529d" />

<img width="839" height="75" alt="image" src="https://github.com/user-attachments/assets/6ff436e2-01b8-4ef6-a5ba-073261e3bf59" />

Created by **amazingb01 (Adiru)**.

## 🚀 Key Features

- **Kernel-Level Memory Purge**: Uses `ntdll.dll` system calls (the same method used by professional tools) to clear Standby Lists, Modified Page Lists, and Priority 0 memory.
- **Working Set Reduction**: Instantly flushes the memory working sets of all running processes to free up physical RAM.
- **Smart Automation**: 
  - **RAM Threshold**: Set a percentage (e.g., 90%) - the app only cleans when memory usage exceeds this limit.
  - **Dynamic Timer**: Set an interval in minutes for periodic checks.
- **Stealth Mode**: Minimizes to the System Tray with double-click restoration.
- **Auto-Installation**: Automatically creates a secure folder in `AppData/Roaming/Amazing Folder` and adds itself to Windows Startup.
- **Multi-Language Support**: Fully localized interface for English, Russian (Русский), Ukrainian (Українська), and Turkish (Türkçe).
- **Persistent Settings**: All your preferences (threshold, interval, selected cleaning modes) are saved in the Windows Registry and persist after reboot.

## 🛠 Cleaning Modes Explained

| Mode | Description |
| :--- | :--- |
| **Working Sets** | Forces applications to release unused physical memory to the page file/cache. |
| **Standby List** | Clears the system file cache that Windows often fails to release, a primary cause of micro-stutters. |
| **Modified List** | Purges modified pages that are waiting to be written to disk. |
| **Priority 0** | Cleans the lowest priority standby pages to maximize available RAM for the game process. |

## 🔗 Connect with me

[![YouTube](https://img.shields.io/badge/YouTube-@adiruaim-FF0000?style=for-the-badge&logo=youtube)](https://www.youtube.com/@adiruaim)
[![TikTok](https://img.shields.io/badge/TikTok-@adiruhs-000000?style=for-the-badge&logo=tiktok)](https://www.tiktok.com/@adiruhs)
[![Donatello](https://img.shields.io/badge/Support-Donatello-orange?style=for-the-badge)](https://donatello.to/Adiru3)

---
