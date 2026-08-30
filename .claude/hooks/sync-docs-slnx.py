#!/usr/bin/env python3
"""PostToolUse(Write) hook.

When a file under ``docs/`` ending in ``.md`` is written, make sure it is listed
in the ``/docs/`` solution folder of ``NovaFE.slnx`` so it shows up in the Visual
Studio Solution Explorer. Idempotent: does nothing if the entry already exists.
"""
import json
import os
import re
import sys


def project_root() -> str | None:
    env = os.environ.get("CLAUDE_PROJECT_DIR")
    if env and os.path.isfile(os.path.join(env, "NovaFE.slnx")):
        return env
    directory = os.getcwd()
    while True:
        if os.path.isfile(os.path.join(directory, "NovaFE.slnx")):
            return directory
        parent = os.path.dirname(directory)
        if parent == directory:
            return None
        directory = parent


def main() -> None:
    try:
        payload = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return

    file_path = (payload.get("tool_input") or {}).get("file_path") or ""
    match = re.search(r"(?:^|/)docs/([^/]+\.md)$", file_path.replace("\\", "/"))
    if not match:
        return

    rel = f"docs/{match.group(1)}"

    root = project_root()
    if not root:
        return
    slnx_path = os.path.join(root, "NovaFE.slnx")

    try:
        with open(slnx_path, "r", encoding="utf-8", newline="") as handle:
            text = handle.read()
    except OSError:
        return

    if re.search(r'<File Path="' + re.escape(rel) + r'"\s*/>', text):
        return  # already registered

    newline = "\r\n" if "\r\n" in text else "\n"
    file_line = re.compile(r'[ \t]*<File Path="(docs/[^"]+\.md)"\s*/>')

    folder_open = re.search(r'([ \t]*)<Folder Name="/docs/">', text)
    if folder_open:
        indent = folder_open.group(1)
        child_indent = indent + "  "
        close = re.search(re.escape(indent) + r"</Folder>", text[folder_open.end():])
        if not close:
            return
        close_start = folder_open.end() + close.start()

        names = file_line.findall(text[folder_open.end():close_start])
        names.append(rel)
        block = newline + newline.join(
            f'{child_indent}<File Path="{name}" />' for name in sorted(set(names))
        ) + newline
        new_text = text[:folder_open.end()] + block + text[close_start:]
    else:
        anchor = text.rfind("</Solution>")
        if anchor == -1:
            return
        block = (
            f'  <Folder Name="/docs/">{newline}'
            f'    <File Path="{rel}" />{newline}'
            f'  </Folder>{newline}'
        )
        new_text = text[:anchor] + block + text[anchor:]

    with open(slnx_path, "w", encoding="utf-8", newline="") as handle:
        handle.write(new_text)

    print(json.dumps({
        "systemMessage": f"docs: {rel} registrado en la carpeta de solución de NovaFE.slnx"
    }))


if __name__ == "__main__":
    main()
