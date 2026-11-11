"""Convierte informe.md en una página HTML autocontenida (imágenes embebidas, modo claro/oscuro)."""

from __future__ import annotations

import base64
import re
from pathlib import Path

import markdown

CSS = """
:root { --bg:#fcfcfb; --ink:#0b0b0b; --ink2:#52514e; --line:#e6e5e1; --card:#ffffff; --accent:#2a78d6; --warn-bg:#fff6e5; --warn-line:#eda100; }
@media (prefers-color-scheme: dark) {
  :root { --bg:#1a1a19; --ink:#f0efec; --ink2:#c3c2b7; --line:#34332f; --card:#222220; --accent:#3987e5; --warn-bg:#2b2412; --warn-line:#c98500; }
}
* { box-sizing: border-box; }
body { margin:0; background:var(--bg); color:var(--ink); font:16px/1.6 system-ui,-apple-system,'Segoe UI',Roboto,sans-serif; }
main { max-width: 920px; margin: 0 auto; padding: 32px 16px 64px; }
h1 { font-size: 1.9rem; margin: 0 0 1rem; }
h2 { font-size: 1.3rem; margin: 2.5rem 0 .75rem; padding-top: 1rem; border-top: 1px solid var(--line); }
p, li { color: var(--ink); }
blockquote { margin: 1rem 0; padding: .75rem 1rem; background: var(--warn-bg); border-left: 4px solid var(--warn-line); border-radius: 6px; }
blockquote p { margin: 0; }
img { max-width: 100%; height: auto; display: block; margin: 1rem auto; border: 1px solid var(--line); border-radius: 10px; }
.table-wrap { overflow-x: auto; margin: 1rem 0; }
table { border-collapse: collapse; width: 100%; font-size: .9rem; }
th, td { padding: .5rem .65rem; border-bottom: 1px solid var(--line); text-align: right; white-space: nowrap; }
th:first-child, td:first-child { text-align: left; }
th { color: var(--ink2); font-weight: 600; font-size: .8rem; text-transform: uppercase; letter-spacing: .03em; }
code { background: var(--card); border: 1px solid var(--line); padding: 0 .3rem; border-radius: 4px; font-size: .9em; }
ul { padding-left: 1.2rem; }
li { margin: .35rem 0; }
"""


def _embed_images(html: str, base: Path) -> str:
    def repl(m: re.Match) -> str:
        src = m.group(1)
        file = base / src
        if not file.exists():
            return m.group(0)
        data = base64.b64encode(file.read_bytes()).decode()
        return f'src="data:image/png;base64,{data}"'

    return re.sub(r'src="([^"]+\.png)"', repl, html)


def build_html(md_path: Path, html_path: Path) -> None:
    md = md_path.read_text(encoding="utf-8")
    body = markdown.markdown(md, extensions=["tables"])
    body = _embed_images(body, md_path.parent)
    body = body.replace("<table>", '<div class="table-wrap"><table>').replace("</table>", "</table></div>")
    html = (
        '<!doctype html><html lang="es"><head><meta charset="utf-8">'
        '<meta name="viewport" content="width=device-width, initial-scale=1">'
        "<title>Análisis de préstamos</title>"
        f"<style>{CSS}</style></head><body><main>{body}</main></body></html>"
    )
    html_path.write_text(html, encoding="utf-8")
