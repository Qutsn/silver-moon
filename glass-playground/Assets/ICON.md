# Application icon

`silvered-moon.ico` is the unmodified approved Windows icon from the 2026-11-17
`design/silvered-moon-icon-release-v2` delivery, verified against its SHA-256 manifest.
It contains all ten supplied sizes (16–256 px). Do not crop, recolor or add a background.

`ApplicationIcon` embeds it in the Windows EXE. The application-level Window style
uses the same Avalonia resource for window/taskbar icons. Shortcuts targeting this
EXE can use its default icon. This project currently ships a portable EXE, not an installer.
