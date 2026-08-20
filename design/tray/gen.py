"""Genera los artboards del icono de bandeja.

El pixel art se escribe aqui como rejillas de 16x16 caracteres, que es exactamente la forma en
que el renderizador de C# lo va a consumir: cada caracter es un pixel y cada pixel se pinta sin
suavizado. Mantener la fuente de verdad en una rejilla y no en un SVG suelto evita el problema
de hoy, donde el icono se dibuja a 32x32 con antialiasing y Windows lo reduce a 16x16.

    python gen.py
"""

import math
from pathlib import Path

HERE = Path(__file__).parent

# Paleta exacta de App.xaml. Nada inventado.
ACCENT = "#62D99C"
WARN = "#F6C85F"
HIGH = "#F49A5A"
CRIT = "#F06464"
PAUSED = "#778292"
GAMING = "#A98AF4"
SURFACE = "#11151D"
RAISED = "#191F2A"
BORDER = "#2B3341"
TEXT = "#F5F7FA"
MUTED = "#99A4B5"
FAINT = "#6B7A90"
TASKBAR = "#1C1C1C"
DIM = "#2F3A47"

FONT_LINK = (
    '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">\n'
    '  <link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
    "family=Space+Grotesk:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;600"
    '&display=swap">'
)

MONO = "'JetBrains Mono', ui-monospace, monospace"

# '#' trazo principal · '=' celda encendida del medidor · '-' celda apagada
NORMAL = {"#": ACCENT, "=": ACCENT, "-": DIM}

EMPTY = ["." * 16] * 16


def rows(*lines):
    """Rellena hasta 16 filas de 16 columnas para no depender de contar a mano."""
    out = [(line + "." * 16)[:16] for line in lines]
    while len(out) < 16:
        out.append("." * 16)
    return out[:16]


def merge(*grids):
    """Superpone rejillas; la ultima que pinta un pixel manda."""
    result = [list("." * 16) for _ in range(16)]
    for grid in grids:
        for y, row in enumerate(grid):
            for x, char in enumerate(row):
                if char != ".":
                    result[y][x] = char
    return ["".join(row) for row in result]


def shift(grid, dy=0, dx=0):
    out = []
    for y in range(16):
        src_y = y - dy
        if 0 <= src_y < 16:
            row = grid[src_y]
            out.append("".join(row[x - dx] if 0 <= x - dx < 16 else "." for x in range(16)))
        else:
            out.append("." * 16)
    return out


# --------------------------------------------------------------------------------------
# La cara. Dos cuadrados por ojos; al parpadear se cierran en chevrones, como >_<
# --------------------------------------------------------------------------------------

EYES_OPEN = rows(
    *["." * 16] * 6,
    "..####....####..",
    "..####....####..",
    "..####....####..",
    "..####....####..",
)

EYES_BLINK = rows(
    *["." * 16] * 6,
    "..##........##..",
    "...##......##...",
    "...##......##...",
    "..##........##..",
)

EYES_SLEEP = rows(
    *["." * 16] * 7,
    "..####....####..",
    "..####....####..",
)

EYES_DEAD = rows(
    *["." * 16] * 6,
    "..#..#....#..#..",
    "...##......##...",
    "...##......##...",
    "..#..#....#..#..",
)

MOUTH_SMILE = rows(
    *["................"] * 9,
    "...#........#...",
    "....########....",
)

MOUTH_FLAT = rows(
    *["................"] * 9,
    "....########....",
)

MOUTH_OPEN = rows(
    *["................"] * 9,
    "....########....",
    "....#......#....",
    "....########....",
)

# Sin boca: a 16 px la mirada sola se lee mas limpia y aguanta mejor el escalado.
FACE = EYES_OPEN
FACE_BLINK = EYES_BLINK
FACE_SLEEP = EYES_SLEEP
FACE_DEAD = EYES_DEAD


def meter(percent, sweep=None):
    """Ya no existe: el presupuesto lo dice el color del icono, no una fila de celdas."""
    return EMPTY


BASE = FACE


# --------------------------------------------------------------------------------------
# Los gags ocupan toda la mitad de arriba. No sale un objeto de la cara: la cara ES el objeto.
# --------------------------------------------------------------------------------------

LAPTOP = rows(
    "................",
    "..############..",
    "..#..........#..",
    "..#..........#..",
    "..#..........#..",
    "..#..........#..",
    "..#..........#..",
    "..############..",
    ".##############.",
)

COFFEE = rows(
    "....#..#..#.....",
    "....#..#..#.....",
    "................",
    "..###########...",
    "..#.........#...",
    "..#.........####",
    "..#.........#..#",
    "..#.........####",
    "..#.........#...",
    "...#########....",
)

PHONE = rows(
    "................",
    "....########....",
    "....#......#....",
    "....#......#....",
    "....#......#....",
    "....#......#....",
    "....#......#....",
    "....#......#....",
    "....#.####.#....",
    "....########....",
)

GAGS = [
    ("Laptop", LAPTOP, "La cara entera se convierte en la pantalla. Es el gag mas legible de los tres."),
    ("Cafe", COFFEE, "Taza con vapor. El vapor es lo unico que se mueve dentro del gag."),
    ("Telefono", PHONE, "El mas alto; se come tambien la fila del medidor si no se cuida el margen."),
]


# --------------------------------------------------------------------------------------
# Las cuatro marcas de la primera ronda, que se quedan como historia
# --------------------------------------------------------------------------------------

MEDIDOR = rows(
    "................",
    "................",
    "................",
    "...........--...",
    "...........--...",
    "........==.--...",
    "........==.--...",
    ".....==.==.--...",
    ".....==.==.--...",
    "..==.==.==.--...",
    "..==.==.==.--...",
    "..==.==.==.--...",
    "..==.==.==.--...",
)

CARITA = rows(
    "................",
    ".....######.....",
    "...##########...",
    "..############..",
    ".##############.",
    ".##############.",
    "####..####..####",
    "####..####..####",
    "################",
    "################",
    ".#####....#####.",
    ".######..######.",
    "..############..",
    "...##########...",
    ".....######.....",
)

ANILLO = rows(
    "................",
    ".....======.....",
    "...==========...",
    "..====....====..",
    ".===........===.",
    ".==..........==.",
    "==............==",
    "==............==",
    "##............##",
    "##............##",
    ".##..........##.",
    ".###........###.",
    "..####....####..",
    "...##########...",
    ".....######.....",
)

PROMPT = rows(
    "................",
    "................",
    "................",
    "................",
    ".##.............",
    ".###............",
    "..###......=====",
    "...###.....=====",
    "...###.....=====",
    "..###......=====",
    ".###............",
    ".##.............",
)

OLD_MARKS = [
    ("A", "Medidor", MEDIDOR, "Dice cuanto llevas gastado sin abrir nada.", "Parece un icono de wifi."),
    ("B", "Carita", CARITA, "Continua la mascota de la primera pestana.", "El dibujo mas apretado a 16 px."),
    ("C", "Anillo", ANILLO, "Forma y medidor en la misma figura.", "Un aro de 2 px se ensucia al escalar."),
    ("D", "Prompt", PROMPT, "Lee 'terminal' al instante.", "No dice nada del gasto por si solo."),
]


# --------------------------------------------------------------------------------------
# Animaciones
# --------------------------------------------------------------------------------------

METER_62 = EMPTY


def with_meter(face_frames, meter_grid=None):
    m = METER_62 if meter_grid is None else meter_grid
    return [merge(f, m) for f in face_frames]


def anim_blink():
    return with_meter([FACE] * 6 + [FACE_BLINK] + [FACE] * 3)


def anim_gag(gag):
    """El objeto sube desde abajo, se queda y baja. La fila del medidor nunca se tapa."""
    frames = [merge(FACE, METER_62)] * 2
    for dy in (10, 6, 3, 0):
        frames.append(merge(shift(gag, dy=dy), METER_62))
    frames += [merge(gag, METER_62)] * 4
    for dy in (3, 6, 10):
        frames.append(merge(shift(gag, dy=dy), METER_62))
    frames += [merge(FACE, METER_62)] * 2
    return frames


def anim_sync():
    """Barrido del medidor mientras el scheduler consulta. La cara no se entera."""
    return [merge(FACE, meter(0, sweep=i)) for i in range(-2, 12)]


def anim_glance():
    """Una mirada rapida cuando el refresh trajo algo distinto."""
    return [
        merge(FACE, METER_62),
        merge(FACE, METER_62),
        merge(shift(EYES_OPEN, dx=1), METER_62),
        merge(shift(EYES_OPEN, dx=1), METER_62),
        merge(shift(EYES_OPEN, dx=-1), METER_62),
        merge(shift(EYES_OPEN, dx=-1), METER_62),
        merge(FACE, METER_62),
        merge(FACE, METER_62),
    ]


def anim_alert():
    """Tres pulsos y se queda quieto. Lo que parpadea es la cara; el medidor no se mueve."""
    on = merge(FACE, meter(96))
    off = meter(96)
    return [on, on, off, on, on, off, on, on, on, on]


ANIMS = {
    "blink": anim_blink(),
    "laptop": anim_gag(LAPTOP),
    "cafe": anim_gag(COFFEE),
    "telefono": anim_gag(PHONE),
    "sync": anim_sync(),
    "glance": anim_glance(),
    "alert": anim_alert(),
}


# --------------------------------------------------------------------------------------
# SVG
# --------------------------------------------------------------------------------------


def svg(grid, size, colors=None, background=None, grid_lines=False):
    colors = colors or NORMAL
    parts = [
        f'<svg width="{size}" height="{size}" viewBox="0 0 16 16" '
        f'shape-rendering="crispEdges" aria-hidden="true">'
    ]
    if background:
        parts.append(f'<rect width="16" height="16" fill="{background}"></rect>')
    for y, row in enumerate(grid):
        for x, char in enumerate(row):
            fill = colors.get(char)
            if fill:
                parts.append(f'<rect x="{x}" y="{y}" width="1" height="1" fill="{fill}"></rect>')
    if grid_lines:
        for i in range(1, 16):
            parts.append(
                f'<rect x="{i}" y="0" width="0.06" height="16" fill="#FFFFFF" opacity="0.10"></rect>'
            )
            parts.append(
                f'<rect x="0" y="{i}" width="16" height="0.06" fill="#FFFFFF" opacity="0.10"></rect>'
            )
    parts.append("</svg>")
    return "".join(parts)


def sprite(frames, scale, colors=None):
    colors = colors or NORMAL
    n = len(frames)
    parts = [
        f'<svg width="{16 * n * scale}" height="{16 * scale}" viewBox="0 0 {16 * n} 16" '
        f'shape-rendering="crispEdges" aria-hidden="true">'
    ]
    for i, frame in enumerate(frames):
        for y, row in enumerate(frame):
            for x, char in enumerate(row):
                fill = colors.get(char)
                if fill:
                    parts.append(
                        f'<rect x="{x + i * 16}" y="{y}" width="1" height="1" fill="{fill}"></rect>'
                    )
    parts.append("</svg>")
    return "".join(parts), n


def live(key, scale=4, ms=140, colors=None):
    frames = ANIMS[key]
    svg_text, n = sprite(frames, scale, colors)
    width = 16 * scale
    return f"""<div style="width: {width}px; height: {width}px; overflow: hidden; background: {TASKBAR}; border-radius: 4px;">
      <div class="anim" style="width: {16 * n * scale}px; animation-name: run-{n}; animation-duration: {n * ms / 1000:.2f}s; animation-timing-function: steps({n}); animation-iteration-count: infinite;">{svg_text}</div>
    </div>"""


def keyframes():
    """Una tira de n cuadros dentro de una ventana de 16 px.

    translateX es relativo al ancho de la propia tira, asi que recorrerla entera (-100 %) en
    steps(n) avanza exactamente un cuadro por paso.
    """
    separator = chr(10) + "    "
    return separator.join(
        f"@keyframes run-{n} {{ from {{ transform: translateX(0); }} "
        f"to {{ transform: translateX(-100%); }} }}"
        for n in sorted({len(f) for f in ANIMS.values()})
    )


def taskbar(grid):
    icons = (
        '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="#C9CCD1" '
        'stroke-width="1.2"><rect x="1.5" y="3.5" width="13" height="8" rx="1"></rect>'
        '<path d="M4 13.5h8"></path></svg>'
        '<svg width="16" height="16" viewBox="0 0 16 16" fill="#C9CCD1">'
        '<path d="M3 6h2.5L9 3v10L5.5 10H3z"></path></svg>'
        '<svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="#C9CCD1" '
        'stroke-width="1.2"><rect x="1.5" y="5.5" width="11" height="6" rx="1"></rect>'
        '<path d="M14 7.5v2"></path><rect x="3" y="7" width="5" height="3" fill="#C9CCD1" '
        'stroke="none"></rect></svg>'
    )
    return f"""<div style="display: flex; align-items: center; gap: 10px; background: {TASKBAR}; border-radius: 4px; padding: 7px 12px;">
      <svg width="16" height="16" viewBox="0 0 16 16" fill="none" stroke="#C9CCD1" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"><path d="M4 10l4-4 4 4"></path></svg>
      {svg(grid, 16)}
      {icons}
      <span style="font-family: 'Segoe UI', system-ui, sans-serif; font-size: 11px; color: #E6E7EA; line-height: 1.15; text-align: center; margin-left: 4px;">1:01 PM<br>8/19/2026</span>
    </div>"""


# --------------------------------------------------------------------------------------
# Plantilla
# --------------------------------------------------------------------------------------

SHELL = """<!doctype html>
<html>
<head>
  <meta charset="utf-8">
  <script src="./support.js"></script>
</head>
<body>
<x-dc>
<helmet>
  {fonts}
  <style>
    body {{ margin: 0; background: #05070B; font-family: "Space Grotesk", "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif; -webkit-font-smoothing: antialiased; }}
    a {{ color: {accent}; }} a:hover {{ color: #8AE7B7; }}
    svg {{ display: block; }}
    .anim {{ will-change: transform; }}
    {keyframes}
    @media (prefers-reduced-motion: reduce) {{ .anim {{ animation: none !important; }} }}
  </style>
</helmet>
{body}
</x-dc>
<script data-dc-script data-props='{{"$preview":{{"width":{w},"height":{h}}}}}'>
class Component extends DCLogic {{
  renderVals() {{
    return {{}};
  }}
}}
</script>
</body>
</html>
"""


def card(inner, w, h, pad="26px 30px"):
    return (
        f'<div style="width: {w}px; height: {h}px; box-sizing: border-box; background: {RAISED}; '
        f'border: 1px solid {BORDER}; border-radius: 14px; padding: {pad}; color: {TEXT}; '
        f'display: flex; flex-direction: column; gap: 18px;">{inner}</div>'
    )


def heading(text):
    return (
        f'<div style="font-size: 10px; font-weight: 600; letter-spacing: 0.16em; color: {FAINT}; '
        f'padding-bottom: 12px; border-bottom: 1px solid {BORDER};">{text}</div>'
    )


def prose(text, color=MUTED, size=11):
    return f'<div style="font-size: {size}px; color: {color}; line-height: 1.55;">{text}</div>'


def label(text):
    return (
        f'<div style="font-size: 10px; font-weight: 600; letter-spacing: 0.14em; '
        f'color: {FAINT};">{text}</div>'
    )


def zoom(grid, size=140, lines=True):
    return (
        f'<div style="background: {SURFACE}; border: 1px solid {BORDER}; border-radius: 8px; '
        f'padding: 9px;">{svg(grid, size, background=TASKBAR, grid_lines=lines)}</div>'
    )


def write(name, body, w, h):
    (HERE / name).write_text(
        SHELL.format(fonts=FONT_LINK, accent=ACCENT, keyframes=keyframes(), body=body, w=w, h=h),
        encoding="utf-8",
    )


# --------------------------------------------------------------------------------------
# Main · la cara
# --------------------------------------------------------------------------------------


def build_main():
    W, H = 1120, 780

    caras = [
        (merge(FACE, METER_62), "Normal", "Dos cuadrados de 4x4. Sin boca y sin contorno."),
        (merge(FACE_BLINK, METER_62), "Parpadeo", "Los ojos se cierran en chevrones hacia dentro: el &gt;_&lt; que pediste."),
        (merge(FACE_SLEEP, METER_62), "Pausado", "Parpados a media asta. Sin animacion."),
        (merge(FACE_DEAD, METER_62), "Proveedor caido", "Ojos en aspa. Estatico mientras dure el fallo."),
    ]
    cells = "".join(
        f"""<div style="display: flex; flex-direction: column; align-items: center; gap: 9px;">
        {zoom(g, 132)}
        <div style="background: {TASKBAR}; border-radius: 4px; padding: 6px;">{svg(g, 16)}</div>
        <span style="font-size: 12px; font-weight: 500;">{nombre}</span>
        <span style="font-size: 10px; color: {FAINT}; text-align: center; line-height: 1.45; max-width: 150px;">{nota}</span>
      </div>"""
        for g, nombre, nota in caras
    )

    anatomia = f"""<div style="display: grid; grid-template-columns: 200px 1fr; gap: 26px; align-items: center;">
      {zoom(BASE, 200)}
      <div style="display: flex; flex-direction: column; gap: 14px;">
        <div style="display: flex; gap: 12px; align-items: flex-start;">
          <span style="font-family: {MONO}; font-size: 10px; color: {ACCENT}; padding-top: 2px;">0-12</span>
          <div>
            <div style="font-size: 12px; font-weight: 500;">La cara</div>
            <div style="font-size: 11px; color: {MUTED}; line-height: 1.5; margin-top: 3px;">Once filas para la personalidad. Aqui viven el parpadeo, las expresiones y los gags.</div>
          </div>
        </div>
        <div style="display: flex; gap: 12px; align-items: flex-start;">
          <span style="font-family: {MONO}; font-size: 10px; color: {ACCENT}; padding-top: 2px;">14</span>
          <div>
            <div style="font-size: 12px; font-weight: 500;">El medidor</div>
            <div style="font-size: 11px; color: {MUTED}; line-height: 1.5; margin-top: 3px;">Catorce celdas con el presupuesto del mes, en una sola fila pegada al canto. Antes eran dos filas justo debajo de los ojos y se leian como una boca. <strong style="color: {TEXT}; font-weight: 500;">Nunca lo tapa un gag.</strong> Asi el icono siempre dice el dato, se este divirtiendo o no.</div>
          </div>
        </div>
        <div style="display: flex; gap: 12px; align-items: flex-start;">
          <span style="font-family: {MONO}; font-size: 10px; color: {FAINT}; padding-top: 2px;">13, 15</span>
          <div>
            <div style="font-size: 12px; font-weight: 500;">Margen</div>
            <div style="font-size: 11px; color: {MUTED}; line-height: 1.5; margin-top: 3px;">Vacio a proposito: Windows recorta un pixel de los bordes en algunas escalas de pantalla.</div>
          </div>
        </div>
      </div>
    </div>"""

    inner = "".join([
        heading("LA CARA · OPCION D SIN CIRCULO, CON EL MEDIDOR DE LA OPCION A"),
        prose(
            "Sin contorno y sin nada alrededor: solo los dos cuadrados, flotando en la barra. "
            "La mitad de arriba es la personalidad; las dos filas de abajo son el dato. "
            "Las dos partes son independientes, y esa separacion es lo que deja meter gags sin perder "
            "nunca de vista el gasto."
        ),
        anatomia,
        f'<div style="border-top: 1px solid {BORDER}; padding-top: 18px; display: flex; flex-direction: column; gap: 16px;">'
        + label("EXPRESIONES")
        + f'<div style="display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 22px;">{cells}</div>'
        + "</div>",
        f'<div style="display: flex; gap: 24px; align-items: center;">{taskbar(BASE)}{live("blink", 3)}</div>',
    ])
    write("Main.dc.html", card(inner, W, H), W, H)


# --------------------------------------------------------------------------------------
# Gags
# --------------------------------------------------------------------------------------


def build_gags():
    W, H = 940, 640
    cells = ""
    for (nombre, grid, nota), key in zip(GAGS, ("laptop", "cafe", "telefono")):
        cells += f"""<div style="display: flex; flex-direction: column; align-items: center; gap: 11px;">
        {zoom(merge(grid, METER_62), 150)}
        <div style="display: flex; gap: 12px; align-items: center;">
          {live(key, 4)}
          <div style="background: {TASKBAR}; border-radius: 4px; padding: 6px;">{svg(merge(grid, METER_62), 16)}</div>
        </div>
        <span style="font-size: 13px; font-weight: 500;">{nombre}</span>
        <span style="font-size: 11px; color: {MUTED}; text-align: center; line-height: 1.5; max-width: 200px;">{nota}</span>
      </div>"""

    inner = "".join([
        heading("GAGS · EL OBJETO OCUPA LA CARA ENTERA"),
        prose(
            "No sale un objeto de detras de la cara: la cara <strong style=\"color: %s; font-weight: 500;\">se convierte</strong> "
            "en el objeto y vuelve. A 16 px es la unica forma de que el dibujo se lea; un objeto pequeno "
            "junto a la cara seria un borron de tres pixeles.<br><br>"
            "Entra desde abajo, se queda algo mas de medio segundo y baja. Las dos filas del medidor "
            "siguen a la vista todo el tiempo."
            % TEXT
        ),
        f'<div style="display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 26px;">{cells}</div>',
        f'<div style="border-top: 1px solid {BORDER}; padding-top: 16px;">'
        + prose(
            "<strong style=\"color: %s; font-weight: 500;\">Cada cuanto.</strong> Uno al azar cada 12-20 minutos, "
            "nunca dos seguidos iguales y nunca mientras haya una alerta activa: un chiste encima de un aviso "
            "es lo unico que puede hacer que el aviso no se lea. Con la pantalla bloqueada o la sesion "
            "remota, ninguno." % TEXT
        )
        + "</div>",
    ])
    write("Gags.dc.html", card(inner, W, H), W, H)


# --------------------------------------------------------------------------------------
# Movimiento funcional
# --------------------------------------------------------------------------------------


def build_movimiento():
    W, H = 1120, 820

    funcionales = [
        (
            "sync",
            "Sincronizando",
            "Las doce celdas se barren de izquierda a derecha mientras el scheduler consulta a los "
            "proveedores. La cara ni se entera.",
            "RefreshScheduler, ya existe",
            "14 cuadros · 1-3 s",
            True,
        ),
        (
            "glance",
            "Llego algo nuevo",
            "Una mirada rapida a un lado y al otro justo despues de un refresh que <em>cambio</em> algo. "
            "Distingue 'revise y todo igual' de 'hay novedades', que es la diferencia que hoy no se ve.",
            "Comparar el snapshot con el anterior",
            "8 cuadros · una vez",
            True,
        ),
        (
            "alert",
            "Aviso",
            "La boca se abre y la cara pulsa tres veces en el color del umbral, y se queda quieta. "
            "El unico momento en que el icono pide que lo mires.",
            "AlertEvaluator, ya existe",
            "10 cuadros · una vez",
            True,
        ),
        (
            None,
            "Proveedor caido",
            "Ojos en aspa mientras algun proveedor lleve fallos consecutivos. Estatico: no se anima "
            "algo que puede durar horas.",
            "ProviderState.ConsecutiveFailures",
            "1 cuadro · sin costo",
            False,
        ),
        (
            None,
            "Pausado o en juego",
            "Parpados a media asta en gris, o la cara normal en morado. Tambien estatico.",
            "PowerMode, ya existe",
            "1 cuadro · sin costo",
            False,
        ),
        (
            None,
            "Pago esta semana",
            "La ultima celda del medidor parpadea despacio durante los tres dias previos a un cargo. "
            "Es el unico aviso que hoy no tiene forma de llegarte sin abrir el popup.",
            "AlertEvaluator.PaymentHorizon, 3 dias",
            "2 cuadros · 1 s",
            False,
        ),
    ]

    filas = ""
    for key, titulo, texto, fuente, coste, _ in funcionales:
        preview = (
            live(key, 3, 120)
            if key
            else f'<div style="width: 48px; height: 48px; background: {TASKBAR}; border-radius: 4px; display: flex; align-items: center; justify-content: center;">{svg(merge(FACE_DEAD, METER_62) if titulo.startswith("Proveedor") else (merge(FACE_SLEEP, METER_62) if titulo.startswith("Pausado") else merge(FACE, meter(62))), 32)}</div>'
        )
        filas += f"""<div style="display: grid; grid-template-columns: 60px 190px 1fr 200px 130px; gap: 18px; align-items: start; padding: 14px 0; border-bottom: 1px solid rgba(255,255,255,0.06);">
        {preview}
        <span style="font-size: 13px; font-weight: 500; padding-top: 12px;">{titulo}</span>
        <span style="font-size: 11px; color: {MUTED}; line-height: 1.55; padding-top: 12px;">{texto}</span>
        <span style="font-family: {MONO}; font-size: 10px; color: {ACCENT}; padding-top: 14px;">{fuente}</span>
        <span style="font-family: {MONO}; font-size: 10px; color: {FAINT}; text-align: right; padding-top: 14px;">{coste}</span>
      </div>"""

    niveles = [
        (
            "Completo",
            "Gags, parpadeo y todo lo funcional.",
            merge(LAPTOP, METER_62),
            True,
        ),
        (
            "Solo util",
            "Sin gags y sin parpadeo. Se mueve unicamente cuando pasa algo que te importa: sincronizacion, novedad, aviso.",
            merge(FACE, meter(62, sweep=5)),
            True,
        ),
        (
            "Quieto",
            "Una sola imagen. Cambia de color y de expresion, pero no se mueve nunca.",
            BASE,
            False,
        ),
    ]
    switch = ""
    for nombre, nota, grid, activo in niveles:
        borde = ACCENT if nombre == "Completo" else BORDER
        switch += f"""<div style="border: 1px solid {borde}; border-radius: 10px; padding: 15px; display: flex; flex-direction: column; gap: 11px; background: {SURFACE};">
        <div style="display: flex; align-items: center; gap: 11px;">
          <div style="background: {TASKBAR}; border-radius: 4px; padding: 6px;">{svg(grid, 32)}</div>
          <span style="font-size: 13px; font-weight: 600;">{nombre}</span>
        </div>
        <span style="font-size: 11px; color: {MUTED}; line-height: 1.5;">{nota}</span>
      </div>"""

    inner = "".join([
        heading("MOVIMIENTO · LO DIVERTIDO Y LO QUE SIRVE"),
        prose(
            "Animar la bandeja es cambiar el icono muchas veces por segundo: cada cuadro es un mapa de "
            "bits y un <span style=\"font-family: %s; color: %s;\">HICON</span> nuevo. A 16x16 es barato, "
            "pero no es gratis. Por eso ninguna de estas corre en bucle: cada una tiene un evento que la "
            "enciende y un final. En reposo, un solo cuadro y cero temporizadores." % (MONO, TEXT)
        ),
        f'<div style="border-top: 1px solid {BORDER};">{filas}</div>',
        f'<div style="padding-top: 6px; display: flex; flex-direction: column; gap: 14px;">'
        + label("EL INTERRUPTOR · MENU DE LA BANDEJA")
        + prose(
            "Tres niveles en vez de un si/no, porque querer que se calle el chiste no es lo mismo que "
            "querer que se calle el aviso.",
            MUTED,
        )
        + f'<div style="display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 16px;">{switch}</div>'
        + "</div>",
    ])
    write("Movimiento.dc.html", card(inner, W, H), W, H)


# --------------------------------------------------------------------------------------
# Estados de color
# --------------------------------------------------------------------------------------


def build_estados():
    W, H = 940, 400
    estados = [
        (ACCENT, FACE, 45, "Normal", "menos del 70 %"),
        (WARN, FACE, 74, "Atencion", "70 %"),
        (HIGH, FACE, 88, "Alto", "85 %"),
        (CRIT, FACE, 97, "Critico", "95 %"),
        (PAUSED, FACE_SLEEP, 45, "Pausado", "sin monitoreo"),
        (GAMING, FACE, 45, "Juego", "solo cache"),
    ]
    cells = ""
    for color, cara, pct, nombre, nota in estados:
        colors = {"#": color, "=": color, "-": DIM}
        grid = merge(cara, meter(pct))
        cells += f"""<div style="display: flex; flex-direction: column; align-items: center; gap: 9px;">
        {zoom(grid, 108, lines=False) if False else f'<div style="background: {SURFACE}; border: 1px solid {BORDER}; border-radius: 8px; padding: 9px;">{svg(grid, 108, colors, background=TASKBAR)}</div>'}
        <div style="background: {TASKBAR}; border-radius: 4px; padding: 6px;">{svg(grid, 16, colors)}</div>
        <span style="font-size: 12px; font-weight: 500;">{nombre}</span>
        <span style="font-size: 10px; color: {FAINT};">{nota}</span>
      </div>"""

    inner = "".join([
        heading("COLORES Y MEDIDOR · LOS UMBRALES NO CAMBIAN"),
        prose(
            "Son los mismos seis colores del codigo, con los mismos cortes. Dos cosas cambian a la vez: "
            "el color tine la cara entera y el medidor se llena. A 16 px el color se lee antes que la "
            "forma, asi que el umbral llega primero."
        ),
        f'<div style="display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 18px;">{cells}</div>',
    ])
    write("Estados.dc.html", card(inner, W, H), W, H)


# --------------------------------------------------------------------------------------
# Historia: las cuatro marcas y la nitidez
# --------------------------------------------------------------------------------------


def build_marcas():
    W, H = 1000, 520
    cols = ""
    for letra, nombre, grid, why, cost in OLD_MARKS:
        elegido = letra == "D"
        cols += f"""<div style="display: flex; flex-direction: column; gap: 12px; opacity: {1 if elegido else 0.55};">
        <div style="display: flex; align-items: baseline; gap: 8px;">
          <span style="font-family: {MONO}; font-size: 11px; color: {ACCENT if elegido else FAINT};">{letra}</span>
          <span style="font-size: 14px; font-weight: 600;">{nombre}</span>
          {'<span style="font-family: ' + MONO + '; font-size: 9px; color: ' + ACCENT + ';">ELEGIDA</span>' if elegido else ''}
        </div>
        {zoom(grid, 150)}
        <div style="font-size: 11px; color: {MUTED}; line-height: 1.5;">{why}</div>
        <div style="font-size: 11px; color: {HIGH}; line-height: 1.5;">{cost}</div>
      </div>"""

    inner = "".join([
        heading("PRIMERA RONDA · DE DONDE SALIO LA CARA"),
        prose(
            "Cuatro direcciones sobre el mismo problema. Gano la D por la forma y la A por el dato: "
            "la propuesta actual es la cara de la D con el medidor de la A en las dos filas de abajo. "
            "Se quedan aqui como historia."
        ),
        f'<div style="display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 24px;">{cols}</div>',
    ])
    write("Marcas.dc.html", card(inner, W, H), W, H)


def build_nitidez():
    W, H = 660, 620
    hoy = f"""<svg width="176" height="176" viewBox="0 0 16 16" aria-hidden="true">
      <rect width="16" height="16" fill="{TASKBAR}"></rect>
      <circle cx="8" cy="8" r="7.5" fill="#232A37"></circle>
      <circle cx="8" cy="8" r="4.5" fill="{ACCENT}"></circle>
    </svg>"""
    inner = "".join([
        heading("POR QUE HOY SE VE COMO UNA MANCHA"),
        prose(
            f"El renderizador dibuja el icono en un lienzo de <span style=\"font-family: {MONO}; color: {TEXT};\">32x32</span> "
            f"con <span style=\"font-family: {MONO}; color: {TEXT};\">SmoothingMode.AntiAlias</span> y deja que Windows lo "
            f"reduzca. Pero la bandeja de esta maquina pide <span style=\"font-family: {MONO}; color: {ACCENT};\">16x16</span>: "
            "cada pixel acaba siendo el promedio de cuatro, y los bordes se convierten en grises. "
            "Ningun dibujo sobrevive a eso, por bonito que sea el original."
        ),
        f"""<div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px;">
      <div style="display: flex; flex-direction: column; gap: 10px;">
        <div style="font-size: 11px; font-weight: 600;">Hoy · circulo suavizado</div>
        <div style="background: {SURFACE}; border: 1px solid {BORDER}; border-radius: 8px; padding: 10px; display: flex; justify-content: center;">{hoy}</div>
        <div style="font-size: 11px; color: {MUTED}; line-height: 1.5;">Los bordes son medias tintas. A 16 px el anillo exterior se lo come todo y solo queda un punto verde.</div>
      </div>
      <div style="display: flex; flex-direction: column; gap: 10px;">
        <div style="font-size: 11px; font-weight: 600;">Propuesta · rejilla exacta</div>
        <div style="background: {SURFACE}; border: 1px solid {BORDER}; border-radius: 8px; padding: 10px; display: flex; justify-content: center;">{svg(BASE, 176, background=TASKBAR, grid_lines=True)}</div>
        <div style="font-size: 11px; color: {MUTED}; line-height: 1.5;">Cada celda es un pixel entero, encendido o apagado. Lo que dibujas es exactamente lo que se ve.</div>
      </div>
    </div>""",
        f'<div style="border-top: 1px solid {BORDER}; padding-top: 16px; display: flex; flex-direction: column; gap: 10px;">'
        + label("LAS TRES REGLAS QUE HAY QUE RESPETAR")
        + prose(
            "1 · Dibujar en 16x16, nunca a 32 para reducir despues.<br>"
            "2 · Sin suavizado ni transparencias parciales: un pixel esta encendido o no.<br>"
            "3 · Trazos de 2 px minimo, o el icono se deshace en cuanto Windows escale a 20 o 24 px "
            "en un monitor con mas ppp."
        )
        + "</div>",
    ])
    write("Nitidez.dc.html", card(inner, W, H), W, H)


CANVAS = """{
  "pages": [
    { "id": "propuesta", "name": "La cara" },
    { "id": "historia", "name": "Primera ronda" }
  ],
  "artboards": [
    { "file": "Main.dc.html", "page": "propuesta", "x": 0, "y": 0, "w": 1120, "h": 780, "title": "La cara" },
    { "file": "Gags.dc.html", "page": "propuesta", "x": 1190, "y": 0, "w": 940, "h": 640, "title": "Gags" },
    { "file": "Movimiento.dc.html", "page": "propuesta", "x": 0, "y": 850, "w": 1120, "h": 820, "title": "Movimiento" },
    { "file": "Estados.dc.html", "page": "propuesta", "x": 1190, "y": 710, "w": 940, "h": 400, "title": "Colores" },
    { "file": "Marcas.dc.html", "page": "historia", "x": 0, "y": 0, "w": 1000, "h": 520, "title": "Las cuatro marcas" },
    { "file": "Nitidez.dc.html", "page": "historia", "x": 1070, "y": 0, "w": 660, "h": 620, "title": "Por que hoy se ve borroso" }
  ],
  "annotations": [
    {
      "id": "brief",
      "page": "propuesta",
      "x": 0,
      "y": -300,
      "w": 640,
      "text": "LO QUE PEDISTE Y COMO QUEDO\\nLa D como cara, sin circulo. Dos cuadrados por ojos y el chevron como parpado: al cerrarlos queda un >_<.\\n\\nLA MEZCLA CON LA A\\nLas dos filas de abajo son el medidor de presupuesto, siempre visibles. Los gags ocupan la cara entera pero nunca tapan esas dos filas: la broma no puede costarte el dato.\\n\\nLO QUE FALTA DECIDIR\\nSi la sonrisa se queda o la cara es solo ojos, y cuales de las seis animaciones funcionales entran en la primera version."
    }
  ],
  "launch": { "view": "canvas", "page": "propuesta" }
}
"""


def main():
    build_main()
    build_gags()
    build_movimiento()
    build_estados()
    build_marcas()
    build_nitidez()
    (HERE / "canvas.json").write_text(CANVAS, encoding="utf-8")
    print("artboards generados en", HERE)


if __name__ == "__main__":
    main()
