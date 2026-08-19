"""Genera los artboards del icono de bandeja.

El pixel art se escribe aqui como rejillas de 16x16 caracteres, que es exactamente la forma en
que el renderizador de C# lo va a consumir: cada caracter es un pixel y cada pixel se pinta sin
suavizado. Mantener la fuente de verdad en una rejilla y no en un SVG suelto evita el problema
de hoy, donde el icono se dibuja a 32x32 con antialiasing y Windows lo reduce a 16x16.

    python gen.py
"""

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

FONT_LINK = (
    '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin="anonymous">\n'
    '  <link rel="stylesheet" href="https://fonts.googleapis.com/css2?'
    'family=Space+Grotesk:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;600'
    '&display=swap">'
)

MONO = "'JetBrains Mono', ui-monospace, monospace"

# --------------------------------------------------------------------------------------
# Las marcas. '#' es el trazo principal, '=' el relleno vivo, '-' el relleno apagado.
# --------------------------------------------------------------------------------------

MEDIDOR = [
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
    "................",
    "................",
    "................",
]

CARITA = [
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
    "................",
]

ANILLO = [
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
    "................",
]

PROMPT = [
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
    "................",
    "................",
    "................",
    "................",
]

MARKS = [
    {
        "id": "medidor",
        "letter": "A",
        "name": "Medidor",
        "grid": MEDIDOR,
        "why": "Dice cuanto llevas gastado sin abrir nada: cuatro barras, las encendidas son el mes.",
        "cost": "Se parece a un icono de senal o de wifi. Identidad casi nula entre los demas iconos.",
    },
    {
        "id": "carita",
        "letter": "B",
        "name": "Carita",
        "grid": CARITA,
        "why": "Es tuyo y nada mas tuyo, y continua la mascota que ya vive en la primera pestana.",
        "cost": "A 16 px la cara es el dibujo mas apretado de los cuatro; los ojos son de 2x2 px.",
    },
    {
        "id": "anillo",
        "letter": "C",
        "name": "Anillo",
        "grid": ANILLO,
        "why": "El aro se llena como reloj: forma reconocible y medidor en la misma figura.",
        "cost": "Un aro de 2 px es lo primero que se ensucia si algun dia hay que escalar a 20 o 24 px.",
    },
    {
        "id": "prompt",
        "letter": "D",
        "name": "Prompt",
        "grid": PROMPT,
        "why": "Chevron y cursor: lee 'terminal' al instante y el bloque parpadea sin esfuerzo.",
        "cost": "No dice nada del gasto por si solo. Todo el peso cae en el color y el movimiento.",
    },
]


def svg(grid, size, colors, background=None, grid_lines=False):
    """Rejilla de 16x16 a SVG. shape-rendering crispEdges: cero suavizado, como en el icono real."""
    cell = size / 16
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


NORMAL = {"#": ACCENT, "=": ACCENT, "-": "#2F3A47"}


def strip(grid):
    """La marca dentro de una barra de tareas, al tamano real."""
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
      {svg(grid, 16, NORMAL)}
      {icons}
      <span style="font-family: 'Segoe UI', system-ui, sans-serif; font-size: 11px; color: #E6E7EA; line-height: 1.15; text-align: center; margin-left: 4px;">1:01 PM<br>8/19/2026</span>
    </div>"""


# --------------------------------------------------------------------------------------
# Cuadros de la animacion de sincronizacion. Un cuadro es una rejilla completa.
# --------------------------------------------------------------------------------------


def frames_medidor():
    out = []
    for lit in range(5):
        frame = []
        for row in MEDIDOR:
            new = []
            for x, char in enumerate(row):
                if char in "=-":
                    bar = (x - 2) // 3
                    new.append("=" if bar < lit else "-")
                else:
                    new.append(char)
            frame.append("".join(new))
        out.append(frame)
    return out + out[-2:0:-1]


def frames_carita():
    """Parpadeo: en el cuadro cerrado la fila superior de los ojos se rellena."""
    closed = list(CARITA)
    closed[6] = "################"
    return [CARITA, CARITA, CARITA, CARITA, CARITA, closed, CARITA, CARITA]


def frames_anillo():
    """Un segmento brillante recorre el aro."""
    ring = [(x, y) for y, row in enumerate(ANILLO) for x, c in enumerate(row) if c != "."]
    import math

    order = sorted(ring, key=lambda p: math.atan2(p[1] - 7.5, p[0] - 7.5))
    out = []
    span = max(1, len(order) // 8)
    for step in range(8):
        head = set(order[(step * span) % len(order):(step * span) % len(order) + span * 2])
        frame = []
        for y, row in enumerate(ANILLO):
            frame.append(
                "".join(
                    ("=" if (x, y) in head else "-") if c != "." else "."
                    for x, c in enumerate(row)
                )
            )
        out.append(frame)
    return out


def frames_prompt():
    on = PROMPT
    off = [row.replace("=", ".") for row in PROMPT]
    return [on, on, on, on, off, off, off, off]


FRAME_SETS = {
    "medidor": frames_medidor(),
    "carita": frames_carita(),
    "anillo": frames_anillo(),
    "prompt": frames_prompt(),
}


def sprite(frames, scale):
    """Todos los cuadros en fila; la ventana de 16 px se desliza con steps()."""
    n = len(frames)
    parts = [
        f'<svg width="{16 * n * scale}" height="{16 * scale}" viewBox="0 0 {16 * n} 16" '
        f'shape-rendering="crispEdges" aria-hidden="true">'
    ]
    for i, frame in enumerate(frames):
        for y, row in enumerate(frame):
            for x, char in enumerate(row):
                fill = NORMAL.get(char)
                if fill:
                    parts.append(
                        f'<rect x="{x + i * 16}" y="{y}" width="1" height="1" fill="{fill}"></rect>'
                    )
    parts.append("</svg>")
    return "".join(parts), n


def live(mark_id, scale=4):
    frames = FRAME_SETS[mark_id]
    svg_text, n = sprite(frames, scale)
    width = 16 * scale
    return f"""<div style="width: {width}px; height: {width}px; overflow: hidden; background: {TASKBAR}; border-radius: 4px;">
      <div class="anim" style="width: {16 * n * scale}px; animation-name: run-{n}; animation-duration: {n * 0.14:.2f}s; animation-timing-function: steps({n}); animation-iteration-count: infinite;">{svg_text}</div>
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
        for n in sorted({len(f) for f in FRAME_SETS.values()})
    )


# --------------------------------------------------------------------------------------
# Plantilla de artboard
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


def body_text(text, color=MUTED, size=11):
    return (
        f'<div style="font-size: {size}px; color: {color}; line-height: 1.55;">{text}</div>'
    )


def write(name, body, w, h):
    (HERE / name).write_text(
        SHELL.format(
            fonts=FONT_LINK,
            accent=ACCENT,
            keyframes=keyframes(),
            body=body,
            w=w,
            h=h,
        ),
        encoding="utf-8",
    )


# --------------------------------------------------------------------------------------
# Main: las cuatro marcas
# --------------------------------------------------------------------------------------

def build_main():
    W, H = 1180, 900
    cols = []
    for mark in MARKS:
        cols.append(f"""<div style="display: flex; flex-direction: column; gap: 14px;">
        <div style="display: flex; align-items: baseline; gap: 8px;">
          <span style="font-family: {MONO}; font-size: 11px; color: {ACCENT};">{mark['letter']}</span>
          <span style="font-size: 15px; font-weight: 600;">{mark['name']}</span>
        </div>
        <div style="display: flex; align-items: flex-end; gap: 16px;">
          <div style="display: flex; flex-direction: column; align-items: center; gap: 6px;">
            <div style="background: {TASKBAR}; border-radius: 4px; padding: 6px;">{svg(mark['grid'], 16, NORMAL)}</div>
            <span style="font-family: {MONO}; font-size: 9px; color: {FAINT};">1x</span>
          </div>
          <div style="display: flex; flex-direction: column; align-items: center; gap: 6px;">
            <div style="background: {TASKBAR}; border-radius: 4px; padding: 8px;">{svg(mark['grid'], 64, NORMAL)}</div>
            <span style="font-family: {MONO}; font-size: 9px; color: {FAINT};">4x</span>
          </div>
        </div>
        <div style="background: {SURFACE}; border: 1px solid {BORDER}; border-radius: 8px; padding: 10px;">{svg(mark['grid'], 176, NORMAL, background=TASKBAR, grid_lines=True)}</div>
        <div style="font-size: 11px; color: {MUTED}; line-height: 1.5;">{mark['why']}</div>
        <div style="font-size: 11px; color: {HIGH}; line-height: 1.5;">Cuesta: {mark['cost']}</div>
      </div>""")

    strips = "".join(
        f"""<div style="display: flex; flex-direction: column; gap: 7px;">
        <span style="font-family: {MONO}; font-size: 9px; color: {FAINT};">{m['letter']} · {m['name']}</span>
        {strip(m['grid'])}
      </div>"""
        for m in MARKS
    )

    inner = "".join([
        heading("CUATRO MARCAS PARA LA BANDEJA · 16 x 16 PX REALES"),
        body_text(
            "Cada una esta dibujada pixel por pixel en una rejilla de 16x16, que es lo que Windows "
            f"muestra en esta maquina a 96 ppp. El <span style=\"font-family: {MONO}; color: {TEXT};\">1x</span> "
            "es el tamano al que la vas a ver de verdad; los otros dos son lupa. "
            "Elige una direccion y la pulimos; no hace falta que te guste entera."
        ),
        f'<div style="display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 26px;">{"".join(cols)}</div>',
        f'<div style="border-top: 1px solid {BORDER}; padding-top: 18px; display: flex; flex-direction: column; gap: 14px;">'
        f'<div style="font-size: 10px; font-weight: 600; letter-spacing: 0.14em; color: {FAINT};">EN TU BARRA, AL TAMANO REAL</div>'
        f'<div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 16px;">{strips}</div>'
        "</div>",
    ])
    write("Main.dc.html", card(inner, W, H), W, H)


# --------------------------------------------------------------------------------------
# Nitidez: por que se ve borroso hoy
# --------------------------------------------------------------------------------------

def build_nitidez():
    W, H = 660, 620
    hoy = f"""<svg width="176" height="176" viewBox="0 0 16 16" aria-hidden="true">
      <rect width="16" height="16" fill="{TASKBAR}"></rect>
      <circle cx="8" cy="8" r="7.5" fill="#232A37"></circle>
      <circle cx="8" cy="8" r="4.5" fill="{ACCENT}"></circle>
    </svg>"""
    inner = "".join([
        heading("POR QUE HOY SE VE COMO UNA MANCHA"),
        body_text(
            f"El renderizador dibuja el icono en un lienzo de <span style=\"font-family: {MONO}; color: {TEXT};\">32x32</span> "
            f"con <span style=\"font-family: {MONO}; color: {TEXT};\">SmoothingMode.AntiAlias</span> y deja que Windows lo "
            f"reduzca. Pero la bandeja de esta maquina pide <span style=\"font-family: {MONO}; color: {ACCENT};\">16x16</span>: "
            "cada pixel del icono acaba siendo el promedio de cuatro, y los bordes se convierten en grises. "
            "Ningun dibujo sobrevive a eso, por bonito que sea el original."
        ),
        f"""<div style="display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 20px;">
      <div style="display: flex; flex-direction: column; gap: 10px;">
        <div style="font-size: 11px; font-weight: 600;">Hoy · circulo suavizado</div>
        <div style="background: {SURFACE}; border: 1px solid {BORDER}; border-radius: 8px; padding: 10px; display: flex; justify-content: center;">{hoy}</div>
        <div style="font-size: 11px; color: {MUTED}; line-height: 1.5;">Los bordes son medias tintas. A 16 px el anillo exterior se come casi todo y solo queda un punto verde.</div>
      </div>
      <div style="display: flex; flex-direction: column; gap: 10px;">
        <div style="font-size: 11px; font-weight: 600;">Propuesta · rejilla exacta</div>
        <div style="background: {SURFACE}; border: 1px solid {BORDER}; border-radius: 8px; padding: 10px; display: flex; justify-content: center;">{svg(CARITA, 176, NORMAL, background=TASKBAR, grid_lines=True)}</div>
        <div style="font-size: 11px; color: {MUTED}; line-height: 1.5;">Cada celda es un pixel entero, encendido o apagado. Lo que dibujas es exactamente lo que se ve.</div>
      </div>
    </div>""",
        f'<div style="border-top: 1px solid {BORDER}; padding-top: 16px; display: flex; flex-direction: column; gap: 10px;">'
        f'<div style="font-size: 10px; font-weight: 600; letter-spacing: 0.14em; color: {FAINT};">LAS TRES REGLAS QUE HAY QUE RESPETAR</div>'
        + body_text(
            "1 · Dibujar en 16x16, nunca a 32 para reducir despues.<br>"
            "2 · Sin suavizado ni transparencias parciales: un pixel esta encendido o no.<br>"
            "3 · Trazos de 2 px de grosor minimo, o el icono se deshace en cuanto Windows escale a 20 o 24 px "
            "en un monitor con mas ppp."
        )
        + "</div>",
    ])
    write("Nitidez.dc.html", card(inner, W, H), W, H)


# --------------------------------------------------------------------------------------
# Movimiento
# --------------------------------------------------------------------------------------

def build_movimiento():
    W, H = 900, 660
    previews = "".join(
        f"""<div style="display: flex; flex-direction: column; align-items: center; gap: 8px;">
        {live(m['id'])}
        <span style="font-family: {MONO}; font-size: 9px; color: {FAINT};">{m['letter']} · {m['name']}</span>
      </div>"""
        for m in MARKS
    )

    momentos = [
        (
            "Sincronizando",
            "Mientras el scheduler consulta a los proveedores. Arranca al empezar el ciclo y para "
            "al terminar, tipicamente 1-3 segundos.",
            f"{len(FRAME_SETS['anillo'])} cuadros · 140 ms",
        ),
        (
            "Aviso",
            "Tres pulsos cuando una alerta pasa el enfriamiento, y se queda quieto. Es el unico "
            "momento en que el icono pide que lo mires.",
            "6 cuadros · una sola vez",
        ),
        (
            "Reposo",
            "Todo el resto del tiempo: un unico icono, cero cuadros, cero temporizadores.",
            "1 cuadro · sin costo",
        ),
    ]
    filas = "".join(
        f"""<div style="display: grid; grid-template-columns: 130px 1fr 150px; gap: 16px; align-items: start; padding: 13px 0; border-bottom: 1px solid rgba(255,255,255,0.06);">
        <span style="font-size: 13px; font-weight: 500;">{titulo}</span>
        <span style="font-size: 11px; color: {MUTED}; line-height: 1.5;">{texto}</span>
        <span style="font-family: {MONO}; font-size: 10px; color: {FAINT}; text-align: right;">{coste}</span>
      </div>"""
        for titulo, texto, coste in momentos
    )

    inner = "".join([
        heading("MOVIMIENTO EN LA BANDEJA · QUE SI SE PUEDE Y QUE CUESTA"),
        body_text(
            "Animar el icono es literalmente cambiarlo muchas veces por segundo: cada cuadro es un "
            "mapa de bits nuevo y un <span style=\"font-family: %s; color: %s;\">HICON</span> que hay que "
            "crear y destruir. A 16x16 es barato, pero no es gratis, y por eso la regla es que "
            "<strong style=\"color: %s; font-weight: 500;\">solo se mueve cuando pasa algo</strong>. "
            "En reposo la aplicacion sigue en 0 %% de CPU, que es el compromiso que ya tiene escrito."
            % (MONO, TEXT, TEXT)
        ),
        f'<div style="display: flex; gap: 26px; align-items: center; justify-content: center; padding: 8px 0;">{previews}</div>'
        + body_text(
            "Arriba, la animacion de sincronizacion de cada marca, a 4x y en bucle para que se vea. "
            "En la barra real dura lo que dure el ciclo y se detiene sola.",
            FAINT,
            10,
        ),
        f'<div style="border-top: 1px solid {BORDER}; padding-top: 12px;">{filas}</div>',
    ])
    write("Movimiento.dc.html", card(inner, W, H), W, H)


# --------------------------------------------------------------------------------------
# Estados de color
# --------------------------------------------------------------------------------------

def build_estados():
    W, H = 900, 380
    estados = [
        (ACCENT, "Normal", "menos del 70 %"),
        (WARN, "Atencion", "70 %"),
        (HIGH, "Alto", "85 %"),
        (CRIT, "Critico", "95 %"),
        (PAUSED, "Pausado", "sin monitoreo"),
        (GAMING, "Juego", "solo cache"),
    ]
    cells = ""
    for color, nombre, nota in estados:
        colors = {"#": color, "=": color, "-": "#2F3A47"}
        cells += f"""<div style="display: flex; flex-direction: column; align-items: center; gap: 9px;">
        <div style="background: {TASKBAR}; border-radius: 6px; padding: 10px;">{svg(CARITA, 48, colors)}</div>
        <span style="font-size: 12px; font-weight: 500;">{nombre}</span>
        <span style="font-size: 10px; color: {FAINT};">{nota}</span>
      </div>"""

    inner = "".join([
        heading("LOS COLORES NO CAMBIAN · SOLO EL DIBUJO"),
        body_text(
            "Son los mismos seis del codigo, con los mismos umbrales. El ejemplo usa la carita, "
            "pero cualquiera de las cuatro marcas se tine igual: la forma dice quien eres, el color "
            "dice como vas."
        ),
        f'<div style="display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 18px;">{cells}</div>',
    ])
    write("Estados.dc.html", card(inner, W, H), W, H)


CANVAS = """{
  "artboards": [
    { "file": "Main.dc.html", "x": 0, "y": 0, "w": 1180, "h": 900, "title": "Cuatro marcas" },
    { "file": "Nitidez.dc.html", "x": 1250, "y": 0, "w": 660, "h": 620, "title": "Por que hoy se ve borroso" },
    { "file": "Movimiento.dc.html", "x": 1250, "y": 680, "w": 900, "h": 660, "title": "Movimiento" },
    { "file": "Estados.dc.html", "x": 0, "y": 970, "w": 900, "h": 380, "title": "Colores de estado" }
  ],
  "annotations": [
    {
      "id": "brief",
      "x": 0,
      "y": -280,
      "w": 620,
      "text": "EL ICONO DE LA BANDEJA\\nHoy es un circulo verde borroso porque se dibuja a 32x32 con suavizado y Windows lo reduce a 16x16. El artboard de la derecha lo explica.\\n\\nQUE HAY QUE DECIDIR\\nUna de las cuatro marcas. Cada una tiene su motivo y su costo escritos debajo; ninguna es la respuesta obvia.\\n\\nSOBRE EL ESTILO DE CLAUDE CODE\\nLo que se toma prestado es el oficio: pixel art nitido de 16x16 y movimiento con proposito. La marca de Claude no se copia; estas cuatro son dibujos propios."
    }
  ],
  "launch": { "view": "canvas" }
}
"""


def main():
    build_main()
    build_nitidez()
    build_movimiento()
    build_estados()
    (HERE / "canvas.json").write_text(CANVAS, encoding="utf-8")
    print("artboards generados en", HERE)


if __name__ == "__main__":
    main()
